using System;
using System.Windows;
using System.IO;
using System.Windows.Threading;
using System.Text;
using Microsoft.Win32;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Controls;

namespace media_player
{
    public partial class MainWindow : Window
    {
        // State flags
        private bool IsPlaying = false;
        private bool IsUserDraggingSlider = false;

        // Playlist to store multiple media files
        private readonly List<string> Playlist = new();

        // Timer for updating UI components like the progress slider
        private readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromSeconds(0.1) };

        // Dialog to allow users to select media files
        private readonly OpenFileDialog MediaOpenDialog = new()
        {
            Title = "Open a media file",
            Filter = "Media Files (*.mp3,*.mp4)|*.mp3;*.mp4"
        };

        // Constructor: Initializes event handlers and starts the timer
        public MainWindow()
        {
            InitializeComponent();

            // Fired when the media finishes loading (used for video dimensions or thumbnail visibility)
            Player.MediaOpened += Player_MediaOpened;

            // Updates slider and time label on a timer
            Timer.Tick += Timer_Tick;
            Timer.Start();
        }

        // Updates progress bar and slider as the media plays
        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (Player.Source != null && Player.NaturalDuration.HasTimeSpan && !IsUserDraggingSlider)
            {
                ProgressSlider.Maximum = Player.NaturalDuration.TimeSpan.TotalSeconds;
                ProgressSlider.Value = Player.Position.TotalSeconds;
            }
        }

        // Extracts album art from audio files and updates the thumbnail image
        private void UpdateThumbnail(string filePath)
        {
            try
            {
                var tfile = TagLib.File.Create(filePath);

                // Check if it's an audio-only file
                bool isAudio = tfile.Properties.MediaTypes.HasFlag(TagLib.MediaTypes.Audio)
                               && !tfile.Properties.MediaTypes.HasFlag(TagLib.MediaTypes.Video);

                if (isAudio)
                {
                    ThumbnailImage.Visibility = Visibility.Visible;

                    // If audio file has embedded artwork, display it
                    if (tfile.Tag.Pictures.Length > 0)
                    {
                        var bin = (byte[]?)tfile.Tag.Pictures[0].Data.Data;
                        if (bin != null)
                        {
                            var image = new System.Windows.Media.Imaging.BitmapImage();
                            using var memStream = new MemoryStream(bin);

                            image.BeginInit();
                            image.StreamSource = memStream;
                            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            image.EndInit();
                            image.Freeze();

                            ThumbnailImage.Source = image;
                            return;
                        }
                    }

                    // No album art found, show default icon
                    ThumbnailImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("pack://application:,,,/media_player;component/thumbnail/music.png"));
                }
                else
                {
                    // Let MediaOpened handle thumbnail hiding for video
                }
            }
            catch
            {
                // If thumbnail reading fails, show default image
                ThumbnailImage.Source = new System.Windows.Media.Imaging.BitmapImage(
                    new Uri("pack://application:,,,/media_player;component/thumbnail/music.png"));
                ThumbnailImage.Visibility = Visibility.Visible;
            }
        }

        // Open button logic: load media and play
        private void OpenBtn_Click(object sender, RoutedEventArgs e)
        {
            if (MediaOpenDialog.ShowDialog() == true)
            {
                Player.Source = new Uri(MediaOpenDialog.FileName);
                TitleLbl.Content = Path.GetFileName(MediaOpenDialog.FileName);

                UpdateThumbnail(MediaOpenDialog.FileName); // Display thumbnail

                Player.Play();
                IsPlaying = true;
                PlayPauseBtn.IsEnabled = true;
                (PlayPauseIcon.Content as TextBlock)!.Text = "⏸️"; // Pause icon
            }
        }

        #region Media Controls

        // Stops playback and resets play state
        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Player.Source != null)
            {
                Player.Stop();
                IsPlaying = false;
                (PlayPauseIcon.Content as TextBlock)!.Text = "▶️"; // Play icon
            }
        }

        // Toggles between play and pause
        private void PlayPauseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (Player.Source == null)
                return;

            if (IsPlaying)
            {
                Player.Pause();
                IsPlaying = false;
                (PlayPauseIcon.Content as TextBlock)!.Text = "▶️";
            }
            else
            {
                Player.Play();
                IsPlaying = true;
                (PlayPauseIcon.Content as TextBlock)!.Text = "⏸️";
            }
        }

        // User starts dragging progress slider
        private void ProgressSlider_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            IsUserDraggingSlider = true;
        }

        // User releases slider: update media position
        private void ProgressSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            IsUserDraggingSlider = false;
            Player.Position = TimeSpan.FromSeconds(ProgressSlider.Value);
        }

        // While dragging, update the time label to show current time
        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            StatusLbl.Text = TimeSpan.FromSeconds(ProgressSlider.Value).ToString(@"hh\:mm\:ss");
        }

        #endregion

        #region Properties

        // Display technical media info like bitrate, duration, resolution
        private void PropertiesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MediaOpenDialog.FileName) || Player.Source == null)
                return;

            try
            {
                var tfile = TagLib.File.Create(MediaOpenDialog.FileName);
                StringBuilder sb = new();

                // Display duration (from MediaElement if available)
                TimeSpan mediaDuration = Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan : TimeSpan.Zero;
                sb.AppendLine("Duration: " + mediaDuration.ToString(@"hh\:mm\:ss"));

                if (tfile.Properties.MediaTypes.HasFlag(TagLib.MediaTypes.Audio))
                {
                    sb.AppendLine("Audio bitrate: " + tfile.Properties.AudioBitrate + " kbps");
                    sb.AppendLine("Audio sample rate: " + tfile.Properties.AudioSampleRate + " Hz");
                    sb.AppendLine("Audio channels: " + (tfile.Properties.AudioChannels == 1 ? "Mono" : "Stereo"));
                }

                if (tfile.Properties.MediaTypes.HasFlag(TagLib.MediaTypes.Video))
                {
                    sb.AppendLine($"Video resolution: {tfile.Properties.VideoWidth} x {tfile.Properties.VideoHeight}");
                }

                MessageBox.Show(sb.ToString(), "Properties");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading file properties: " + ex.Message);
            }
        }

        // Adds multiple media files to playlist and UI listbox
        private void AddToPlaylistBtn_Click(object sender, RoutedEventArgs e)
        {
            MediaOpenDialog.Multiselect = true;

            if (MediaOpenDialog.ShowDialog() == true)
            {
                foreach (var file in MediaOpenDialog.FileNames)
                {
                    Playlist.Add(file);
                    PlaylistBox.Items.Add(Path.GetFileName(file)); // Show only filename in listbox
                }
            }
        }

        // When a playlist item is clicked, load and play it
        private void PlaylistBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int index = PlaylistBox.SelectedIndex;
            if (index >= 0 && index < Playlist.Count)
            {
                string selectedFile = Playlist[index];
                Player.Source = new Uri(selectedFile);
                TitleLbl.Content = Path.GetFileName(selectedFile);

                UpdateThumbnail(selectedFile); // Refresh thumbnail
                Player.Play();
                IsPlaying = true;
                PlayPauseBtn.IsEnabled = true;
                (PlayPauseIcon.Content as TextBlock)!.Text = "⏸️";
            }
        }

        // When video file is opened, hide the audio thumbnail and allow proper scaling
        private void Player_MediaOpened(object? sender, EventArgs e)
        {
            if (Player.NaturalVideoWidth <= 0 || Player.NaturalVideoHeight <= 0)
                return;

            Player.ClearValue(WidthProperty);
            Player.ClearValue(HeightProperty);

            ThumbnailImage.Visibility = Visibility.Collapsed;
            Player.Stretch = Stretch.Uniform; // Maintain aspect ratio
        }

        #endregion
    }
}
