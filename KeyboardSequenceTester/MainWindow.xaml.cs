using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using WpfAnimatedGif;

namespace KeyboardSequenceTester
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private SolidColorBrush failColor = new SolidColorBrush(Color.FromRgb(255, 0, 0));

        private SolidColorBrush successColor = new SolidColorBrush(Color.FromRgb(0, 255, 0));

        private XDocument xmlSequences;

        private List<Sequence> allSequences = new List<Sequence>();

        private Sequence selectedSequence;

        private int sequenceIndex = 0;

        private Stopwatch timeWatcher;

        private bool exactMode = false;

        private bool keyDownMode = false;

        private string ResourcesFolder = System.AppDomain.CurrentDomain.BaseDirectory + "Resources\\";

        private string IconsFolder = System.AppDomain.CurrentDomain.BaseDirectory + "Resources\\icons\\";
        
        /// <summary>
        /// base gcd
        /// </summary>
        private double RAW_GCD = 2.5;
        
        /// <summary>
        /// mandatory delay after any cast, even ogcd
        /// </summary>
        private double ANIMATION_LOCK_DELAY = 0.5;
        
        /// <summary>
        /// gcd with skill speed 
        /// </summary>
        private double COMPUTED_GCD = 2.5;

        /// <summary>
        /// base stat minimum 354
        /// </summary>
        private double SKILLSPEED = 354;

        /// <summary>
        /// 
        /// </summary>
        public bool IsGCD = false;

        /// <summary>
        /// 
        /// </summary>
        private bool IsCasting = false;

        /// <summary>
        /// 
        /// </summary>
        private bool ShowCastAnimation = true;

        /// <summary>
        /// 
        /// </summary>
        private DoubleAnimation CastBarAnimation { get; set; }

        /// <summary>
        /// 
        /// </summary>
        private DoubleAnimation GCDAnimation { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public MainWindow()
        {
            //
            InitializeComponent();

            //
            OnLoad();

            //
            this.PreviewKeyDown += MainWindow_KeyEventControl;

            //
            SequenceProgress_Bar.Width = 0;
            CastBarRectangle_Bar.Width = 0;
            CastBarRectangle_GCD_Bar.Width = 0;

            //
            COMPUTED_GCD = RAW_GCD - (0.01 * (SKILLSPEED - 354.0) / 26.5);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_KeyEventControl(object sender, KeyEventArgs e)
        {
            //System.Console.WriteLine("Keyboard.Modifiers : " + Keyboard.Modifiers);
            if (e.Key == Key.Space && Keyboard.Modifiers == 0)
            {
                if (selectedSequence == null) return;
                if (timeWatcher != null && timeWatcher.IsRunning) stopSequence();
                else startSequence();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        private void OnLoad()
        {
            try
            {
                xmlSequences = XDocument.Load(ResourcesFolder + "sequences.xml");

                var skillsList = (from key in xmlSequences.Descendants("skill")
                                  select new Skill()
                                      {
                                          Name = key.Attribute("name").Value,
                                          Icon = key.Attribute("icon") != null ? key.Attribute("icon").Value : "no_icon.png",
                                          Shortcut = key.Attribute("shortcut").Value,
                                          CastingTime = ConvertToDouble(key.Attribute("castingtime").Value)
                                      }).ToList<Skill>();

                var sequencesNames = (from key in xmlSequences.Descendants("sequence") select key.Attribute("name").Value).ToList();

                allSequences = (from key in xmlSequences.Descendants("sequence")
                                select new Sequence()
                                        {
                                            Name = key.Attribute("name").Value,
                                            Timer = ConvertToDouble(key.Attribute("timer").Value),
                                            Icon = key.Attribute("icon") != null ? key.Attribute("icon").Value : "no_icon.png",

                                            Skills = (
                                                from seqSkill in ( ( from subkey in key.Descendants("key") select subkey.Attribute("skill").Value).ToList() )
                                                join eSkill in skillsList
                                                on seqSkill equals eSkill.Name
                                                select eSkill).ToList<Skill>()

                                        }
                               ).ToList<Sequence>();
                
                if (sequencesNames.Count > 0)
                {
                    this.SequencesComboBox.SelectionChanged += SequencesComboBox_SelectionChanged;
                    this.SequencesComboBox.ItemsSource = sequencesNames;
                }

            }
            catch (Exception e)
            {
                System.Console.WriteLine("Exception : " + e.Message);
                //throw e;
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        public static double ConvertToDouble(string Value)
        {

            if (Value == null)
            {
                return 0;
            }
            else
            {
                Value = Value.Replace(".", ",");
                double OutVal;
                double.TryParse(Value, out OutVal);

                if (double.IsNaN(OutVal) || double.IsInfinity(OutVal))
                {
                    return 0;
                }
                return OutVal;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SequencesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //
            if (timeWatcher != null && timeWatcher.IsRunning) stopSequence();

            selectedSequence = allSequences.Where(s => s.Name == SequencesComboBox.SelectedValue.ToString()).FirstOrDefault();

            if (selectedSequence != null)
            {
                SequenceIcon.Source = new BitmapImage(new Uri(IconsFolder + selectedSequence.Icon));
                UpdateNextIcon(0);
            }
            /*
            System.Console.WriteLine("SequenceIcon.Source : " + SequenceIcon.Source);
            System.Console.WriteLine("ImageKeyIcon.Source: " + ImageKeyIcon.Source);
            System.Console.WriteLine("ImageNextKeyIcon.Source : " + ImageNextKeyIcon.Source);
            */
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSequence == null) return;

            if (StartButton.Content.ToString() == "Start")
            {
                startSequence();
            }
            else
            {
                stopSequence();
            }
        }


        /// <summary>
        /// 
        /// </summary>
        private void startSequence()
        {
            sequenceIndex = 0;

            SetBarProgress(0);

            timeWatcher = new Stopwatch();
            timeWatcher.Start();
            StartButton.Content = "Stop";

            if (keyDownMode)
            {
                this.KeyDown += MainWindow_KeyEvent;
                this.MouseDown += MainWindow_MouseEvent;
            }
            else
            {
                this.KeyUp += MainWindow_KeyEvent;
                this.MouseUp += MainWindow_MouseEvent;
            }

            this.pnlTextBox2.Text = String.Format("Starting sequence {0} - {1} key(s) expected", selectedSequence.Name, selectedSequence.Skills.Count);

            this.pnlTextBox2.IsEnabled = false;

            //desactivate combobox
            this.SequencesComboBox.SelectionChanged -= SequencesComboBox_SelectionChanged;

            UpdateNextIcon(sequenceIndex);
        }

        /// <summary>
        /// 
        /// </summary>
        private void stopSequence()
        {
            timeWatcher.Stop();
            StartButton.Content = "Start";

            this.KeyDown -= MainWindow_KeyEvent;
            this.MouseDown -= MainWindow_MouseEvent;

            this.KeyUp -= MainWindow_KeyEvent;
            this.MouseUp -= MainWindow_MouseEvent;

            this.pnlTextBox2.IsEnabled = true;
            //active the combobox
            this.SequencesComboBox.SelectionChanged += SequencesComboBox_SelectionChanged;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        private void SetBarProgress(double percent)
        {
            //System.Console.WriteLine("SetBarProgress : " + percent + "%");
            //System.Console.WriteLine("SequenceProgress_Background.Width : " + SequenceProgress_Background.Width.ToString());

            double newValue = (SequenceProgress.Width * (percent / 100));
            SequenceProgress_Bar.Width = newValue;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_KeyEvent(object sender, KeyEventArgs e)
        {
            
            var lowerRealKey = e.Key.ToString().ToLower();
            var lowerSystemKey = e.SystemKey.ToString().ToLower();
            
            /*
            System.Console.WriteLine("lowerRealKey : " + lowerRealKey);
            System.Console.WriteLine("lowerSystemKey : " + lowerSystemKey);
            */

            //ALT+x
            if (lowerRealKey == "system")
            {
                //only ALT+modifier, don't need to go further
                if (lowerSystemKey.IndexOf("ctrl") > -1 || lowerSystemKey.IndexOf("alt") > -1 || lowerSystemKey.IndexOf("shift") > -1)
                {
                    return;
                }
            }

            //only modifier key pressed => return 
            if (lowerRealKey.IndexOf("ctrl") > -1 || lowerRealKey.IndexOf("alt") > -1 || lowerRealKey.IndexOf("shift") > -1)
            {
                return;
            }

            var input = "";

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                input = "Ctrl+";
            }

            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt) || Keyboard.IsKeyDown(Key.System))
            {
                input = input + "Alt+";
            }

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                input = input + "Shift+";
            }

            //
            if (lowerRealKey == "system")
            {
                input = input + e.SystemKey.ToString();
            }
            else
            {
                input = input + e.Key.ToString();
            }

            //
            if (input != "")
            {
                double timeStamp = timeWatcher.Elapsed.TotalSeconds;
                pnlTextBox2.Text = String.Format("{0:0.00}", timeStamp) + " : " + input + "\n" + pnlTextBox2.Text;
            }

            checkInput(input);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_MouseEvent(object sender, MouseButtonEventArgs e)
        {

            var input = "";
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                input = "Ctrl+";
            }

            if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
            {
                input = input + "Alt+";
            }

            if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
            {
                input = input + "Shift+";
            }

            double timeStamp = timeWatcher.Elapsed.TotalSeconds;

            input = input + e.ChangedButton.ToString() + "Clic";

            //System.Console.WriteLine("pnlMainGrid_MouseUp : " + timeStamp.ToString() + " : " + input);
            pnlTextBox2.Text = String.Format("{0:0.00}", timeStamp) + " : " + input + "\n" + pnlTextBox2.Text;

            checkInput(input);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        private void checkInput(string input)
        {
            if (String.IsNullOrEmpty(input)) return;

            var refInput = selectedSequence.Skills[sequenceIndex].Shortcut.ToLower();
            input = input.ToLower();

            if (refInput == input && !IsCasting && !IsGCD)
            {
                //
                if (ShowCastAnimation) 
                {
                    StartCastBarAnimation();
                    StartGCDBarAnimation();
                }

                sequenceIndex++;
                double percent = (double)sequenceIndex / (double)selectedSequence.Skills.Count * 100;
                percent = Math.Truncate(percent * 100) / 100;
                //System.Console.WriteLine("percent : " + percent);
                SetBarProgress(percent);

                if (sequenceIndex == selectedSequence.Skills.Count)
                {
                    showResults();
                    stopSequence();
                }
                else
                {
                    UpdateNextIcon(sequenceIndex);
                }

                if (timeWatcher.Elapsed.TotalSeconds > selectedSequence.Timer)
                {
                    this.SequenceProgress_Bar.Fill = failColor;
                }
                else
                {
                    this.SequenceProgress_Bar.Fill = successColor;
                }
            }
            else
            {
                //bad input in exact mode = fail
                if (exactMode)
                {
                    this.SequenceProgress_Bar.Fill = failColor;
                }
            }
        }


        /// <summary>
        /// バーのアニメーションを開始する
        /// </summary>
        public void StartCastBarAnimation()
        {
            
            Skill currentSkill = selectedSequence.Skills[sequenceIndex];

            if (currentSkill.CastingTime == 0)
            {
                return;
            }

            CastBarRectangle_Bar.Visibility = System.Windows.Visibility.Visible;

            var computedCastingTime = ComputeCastingTime(currentSkill.CastingTime);

            CastBarRectangle_Bar.Width = CastBarRectangle.Width;

            if (CastBarAnimation == null)
            {
                CastBarAnimation = new DoubleAnimation();
            }
                
            CastBarAnimation.AutoReverse = false;
            
            CastBarAnimation.From = 0.0;
            CastBarAnimation.To = 1.0;

            CastBarAnimation.Duration = new Duration(TimeSpan.FromSeconds(computedCastingTime));

            ScaleTransform st = new ScaleTransform();

            CastBarRectangle_Bar.RenderTransform = st;
            CastBarRectangle_Bar.RenderTransformOrigin = new Point(0.0, 0.5);

            IsCasting = true;
            CastBarAnimation.Completed += new EventHandler(OnCastBarAnimationCompleted);
            st.BeginAnimation(ScaleTransform.ScaleXProperty, CastBarAnimation);

            //
            ImageBehavior.SetAnimatedSource(ImageCastedSkill, null);

            ImageCastedSkill.Visibility = System.Windows.Visibility.Visible;
            ImageCastedSkill.Source = new BitmapImage(new Uri(IconsFolder + currentSkill.Icon));
            ImageBehavior.SetAnimatedSource(ImageCastedSkill, ImageCastedSkill.Source);

            //System.Console.WriteLine("BarAnimation.GetCurrentValue : " + BarAnimation.GetCurrentValue(BarAnimation.From, BarAnimation.To, BarAnimation.));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnCastBarAnimationCompleted(object sender, EventArgs e)
        {
            IsCasting = false;
            
            CastBarRectangle_Bar.Visibility = System.Windows.Visibility.Hidden;
            ImageCastedSkill.Visibility = System.Windows.Visibility.Hidden;

            CastBarAnimation.Completed -= OnCastBarAnimationCompleted;
            System.Console.WriteLine("OnCastBarAnimationCompleted");
        }


        public void StartGCDBarAnimation()
        {
            
            Skill currentSkill = selectedSequence.Skills[sequenceIndex];

            CastBarRectangle_GCD_Bar.Visibility = System.Windows.Visibility.Visible;

            CastBarRectangle_GCD_Bar.Width = CastBarRectangle.Width;

            if (GCDAnimation == null)
            {
                GCDAnimation = new DoubleAnimation();
            }

            GCDAnimation.AutoReverse = false;

            GCDAnimation.From = 0.0;
            GCDAnimation.To = 1.0;

            //
            if (currentSkill.CastingTime == 0)
            {
                GCDAnimation.Duration = new Duration(TimeSpan.FromSeconds(ANIMATION_LOCK_DELAY));
            }
            else
            {
                //GCDAnimation.Duration = new Duration(TimeSpan.FromSeconds(COMPUTED_GCD));
                GCDAnimation.Duration = new Duration(TimeSpan.FromSeconds(ANIMATION_LOCK_DELAY));
            }
            
            ScaleTransform st = new ScaleTransform();

            CastBarRectangle_GCD_Bar.RenderTransform = st;
            CastBarRectangle_GCD_Bar.RenderTransformOrigin = new Point(0.0, 0.5);

            IsGCD = true;
            GCDAnimation.Completed += new EventHandler(OnGCDBarAnimationCompleted);
            st.BeginAnimation(ScaleTransform.ScaleXProperty, GCDAnimation);           


        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnGCDBarAnimationCompleted(object sender, EventArgs e)
        {
            IsGCD = false;
            CastBarRectangle_GCD_Bar.Visibility = System.Windows.Visibility.Hidden;
            GCDAnimation.Completed -= OnGCDBarAnimationCompleted;
            System.Console.WriteLine("OnGCDBarAnimationCompleted");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="castTime"></param>
        /// <returns></returns>
        private double ComputeCastingTime(double castTime)
        {
            double ss_castime = (castTime - (0.01 * (SKILLSPEED - 354.0) / 26.5));
            return ss_castime;
        }

        /// <summary>
        /// 
        /// </summary>
        private void showResults()
        {
            NextKeyLabel.Content = "Sequence Ended!";
            pnlTextBox2.Text = "Sequence completed in : " + String.Format("{0:0.00} seconds", timeWatcher.Elapsed.TotalSeconds) + "\n" + pnlTextBox2.Text;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        private void UpdateNextIcon(int index)
        {
            NextKeyLabel.Content = selectedSequence.Skills[index].Shortcut + String.Format(" - ({0})", selectedSequence.Skills.Count > index + 1 ? selectedSequence.Skills[index + 1].Shortcut : "");

            //unload gif
            ImageBehavior.SetAnimatedSource(ImageKeyIcon, null);
            ImageBehavior.SetAnimatedSource(ImageNextKeyIcon, null);
            
            //
            ImageKeyIcon.Source = new BitmapImage(new Uri(IconsFolder + selectedSequence.Skills[index].Icon));
            ImageBehavior.SetAnimatedSource(ImageKeyIcon, ImageKeyIcon.Source);

            if (selectedSequence.Skills.Count > index + 1)
            {
                ImageNextKeyIcon.Source = new BitmapImage(new Uri(IconsFolder + selectedSequence.Skills[index + 1].Icon));
            }
            else
            {
                ImageNextKeyIcon.Source = new BitmapImage(new Uri(IconsFolder + "no_icon.png"));
            }

            ImageBehavior.SetAnimatedSource(ImageNextKeyIcon, ImageNextKeyIcon.Source);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckBoxExact_Changed(object sender, RoutedEventArgs e)
        {
            exactMode = CheckBoxExact.IsChecked.Value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CheckBoxKeyDown_Changed(object sender, RoutedEventArgs e)
        {
            keyDownMode = CheckBoxKeyDown.IsChecked.Value;
        }

        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ScrollBarSkillSpeed_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SKILLSPEED = ScrollBarSkillSpeed.Value;
            COMPUTED_GCD = ComputeCastingTime(RAW_GCD);
        }

        private void TextBoxSkillSpeed_TextChanged(object sender, TextChangedEventArgs e)
        {

            ScrollBarSkillSpeed.Value = ConvertToDouble(TextBoxSkillSpeed.Text);
        }

        private void CheckBoxShowCastAnimation_Changed(object sender, RoutedEventArgs e)
        {
            ShowCastAnimation = CheckBoxShowCastAnimation.IsChecked.Value;

            if ( !ShowCastAnimation )
            {
                CastBarRectangle_Bar.Visibility = System.Windows.Visibility.Hidden;
                CastBarRectangle_GCD_Bar.Visibility = System.Windows.Visibility.Hidden;
                ImageCastedSkill.Visibility = System.Windows.Visibility.Hidden;
            }
        }
    }

    
    /// <summary>
    /// 
    /// </summary>
    public class Sequence
    {
        public Sequence()
        {
            Skills = new List<Skill>();
        }

        public string Name { get; set; }
        public string Icon { get; set; }
        public double Timer { get; set; }
        public List<Skill> Skills { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class SequenceKey
    {
        public SequenceKey()
        {
        }

        public string Name { get; set; }
        public string Icon { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class Skill
    {
        public Skill()
        {
        }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Shortcut { get; set; }
        public double CastingTime{ get; set; }
    }

}
