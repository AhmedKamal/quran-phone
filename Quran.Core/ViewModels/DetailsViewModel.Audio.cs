// --------------------------------------------------------------------------------------------------------------------
// <summary>
//    Defines the DetailsViewModel type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Cirrious.MvvmCross.ViewModels;
using Quran.Core.Common;
using Quran.Core.Data;
using Quran.Core.Properties;
using Quran.Core.Utils;

namespace Quran.Core.ViewModels
{
    /// <summary>
    /// Define the DetailsViewModel type.
    /// </summary>
    public partial class DetailsViewModel : ViewModelWithDownload
    {
        #region Properties
        private AudioState audioPlayerState;
        public AudioState AudioPlayerState
        {
            get { return audioPlayerState; }
            set
            {
                if (value == audioPlayerState)
                    return;

                audioPlayerState = value;
                base.RaisePropertyChanged(() => AudioPlayerState);
            }
        }

        private bool isDownloadingAudio;
        public bool IsDownloadingAudio
        {
            get { return isDownloadingAudio; }
            set
            {
                if (value == isDownloadingAudio)
                    return;

                isDownloadingAudio = value;
                base.RaisePropertyChanged(() => IsDownloadingAudio);
            }
        }

        private int audioDownloadProgress;
        public int AudioDownloadProgress
        {
            get { return audioDownloadProgress; }
            set
            {
                if (value == audioDownloadProgress)
                    return;

                audioDownloadProgress = value;
                base.RaisePropertyChanged(() => AudioDownloadProgress);
            }
        }

        private bool repeatAudio;
        public bool RepeatAudio
        {
            get { return repeatAudio; }
            set
            {
                if (value == repeatAudio)
                    return;

                repeatAudio = value;

                // saving to setting utils
                SettingsUtils.Set(Constants.PREF_AUDIO_REPEAT, value);

                ResetRepeatState();

                base.RaisePropertyChanged(() => RepeatAudio);
            }
        }

        private void ResetRepeatState()
        {
            if (AudioPlayerState == AudioState.Playing)
            {
                var position = QuranApp.NativeProvider.AudioProvider.Position;
                Stop();
                Play();
                QuranApp.NativeProvider.AudioProvider.Position = position;
            }
        }

        #endregion Properties

        #region Audio Control Commands
        MvxCommand playCommand;
        /// <summary>
        /// Returns a play command
        /// </summary>
        public ICommand PlayCommand
        {
            get
            {
                if (playCommand == null)
                {
                    playCommand = new MvxCommand(Play);
                }
                return playCommand;
            }
        }
        MvxCommand pauseCommand;
        /// <summary>
        /// Returns a pause command
        /// </summary>
        public ICommand PauseCommand
        {
            get
            {
                if (pauseCommand == null)
                {
                    pauseCommand = new MvxCommand(Pause);
                }
                return pauseCommand;
            }
        }
        MvxCommand stopCommand;
        /// <summary>
        /// Returns a stop command
        /// </summary>
        public ICommand StopCommand
        {
            get
            {
                if (stopCommand == null)
                {
                    stopCommand = new MvxCommand(Stop);
                }
                return stopCommand;
            }
        }
        MvxCommand settingsCommand;
        /// <summary>
        /// Returns a settings command
        /// </summary>
        public ICommand SettingsCommand
        {
            get
            {
                if (settingsCommand == null)
                {
                    settingsCommand = new MvxCommand(Settings);
                }
                return settingsCommand;
            }
        }
        public event EventHandler NavigateToSettings;
        #endregion Commands

        #region Audio

        public void Play()
        {
            if (QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Paused)
            {
                QuranApp.NativeProvider.AudioProvider.Play();
            }
            else
            {
                var selectedAyah = QuranApp.DetailsViewModel.SelectedAyah;
                if (selectedAyah == null)
                {
                    var bounds = QuranUtils.GetPageBounds(QuranApp.DetailsViewModel.CurrentPageNumber);
                    selectedAyah = new QuranAyah
                        {
                            Surah = bounds[0],
                            Ayah = bounds[1]
                        };
                    if (selectedAyah.Ayah == 1 && selectedAyah.Surah != Constants.SURA_TAWBA &&
                        selectedAyah.Surah != Constants.SURA_FIRST)
                    {
                        selectedAyah.Ayah = 0;
                    }
                }
                if (QuranUtils.IsValid(selectedAyah))
                {
                    PlayFromAyah(selectedAyah.Surah, selectedAyah.Ayah);
                }
            }
        }

        public void Pause()
        {
            QuranApp.NativeProvider.AudioProvider.Pause();
        }

        public void Stop()
        {
            QuranApp.NativeProvider.AudioProvider.Stop();
        }

        public void Settings()
        {
            if (NavigateToSettings != null)
            {
                NavigateToSettings(this, null);
            }
        }

        public void PlayFromAyah(int startSura, int startAyah)
        {
            int currentQari = AudioUtils.GetReciterIdByName(SettingsUtils.Get<string>(Constants.PREF_ACTIVE_QARI));
            if (currentQari == -1)
                return;

            var shouldRepeat = SettingsUtils.Get<bool>(Constants.PREF_AUDIO_REPEAT);
            var repeatAmount = SettingsUtils.Get<RepeatAmount>(Constants.PREF_REPEAT_AMOUNT);
            var repeatTimes = SettingsUtils.Get<int>(Constants.PREF_REPEAT_TIMES);
            var repeat = new RepeatInfo();
            if (shouldRepeat)
            {
                repeat.RepeatAmount = repeatAmount;
                repeat.RepeatCount = repeatTimes;
            }
            var lookaheadAmount = SettingsUtils.Get<AudioDownloadAmount>(Constants.PREF_DOWNLOAD_AMOUNT);
            var ayah = new QuranAyah(startSura, startAyah);
            var request = new AudioRequest(currentQari, ayah, repeat, 0, lookaheadAmount);

            if (SettingsUtils.Get<bool>(Constants.PREF_PREFER_STREAMING))
            {
                PlayStreaming(request);
            }
            else
            {
                DownloadAndPlayAudioRequest(request);
            }
        }

        private void PlayStreaming(AudioRequest request)
        {
            //TODO: download database

            //TODO: play audio
        }

        private async void DownloadAndPlayAudioRequest(AudioRequest request)
        {
            if (request == null || this.ActiveDownload.IsDownloading)
            {
                return;
            }

            var result = await DownloadAudioRequest(request);

            if (!result)
            {
                QuranApp.NativeProvider.ShowErrorMessageBox("Something went wrong. Unable to download audio.");
            }
            else
            {
                var path = AudioUtils.GetLocalPathForAyah(request.CurrentAyah.Ayah == 0 ? new QuranAyah(1, 1) : request.CurrentAyah, request.Reciter);
                var title = request.CurrentAyah.Ayah == 0 ? "Bismillah" : QuranUtils.GetSurahAyahString(request.CurrentAyah);
                QuranApp.NativeProvider.AudioProvider.SetTrack(new Uri(path, UriKind.Relative), title, request.Reciter.Name, "Quran", null,
                    request.ToString());
            }
        }

        private async Task<bool> DownloadAudioRequest(AudioRequest request)
        {
            bool result = true;
            // checking if there is aya position file
            if (!FileUtils.HaveAyaPositionFile())
            {
                result = await DownloadAyahPositionFile();
            }

            // checking if need to download gapless database file
            if (result && AudioUtils.ShouldDownloadGaplessDatabase(request))
            {
                string url = request.Reciter.GaplessDatabasePath;
                string destination = request.Reciter.LocalPath;
                // start the download
                result = await this.ActiveDownload.Download(url, destination, AppResources.loading_data);
            }

            // checking if need to download mp3
            if (result && !AudioUtils.HaveAllFiles(request))
            {
                string url = request.Reciter.ServerUrl;
                string destination = request.Reciter.LocalPath;
                FileUtils.MakeDirectory(destination);

                if (request.Reciter.IsGapless)
                    result = await AudioUtils.DownloadGaplessRange(url, destination, request.FromAyah, request.ToAyah);
                else
                    result = await AudioUtils.DownloadRange(request);
            }
            return result;
        }

        private async void AudioProvider_StateChanged(object sender, EventArgs e)
        {
            if (QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Stopped ||
                    QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Unknown ||
                QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Error)
            {
                await Task.Delay(500);
                // Check if still stopped
                if (QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Stopped ||
                    QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Unknown ||
                QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Error)
                {
                    AudioPlayerState = AudioState.Stopped;
                }
            }
            else if (QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Paused)
            {
                AudioPlayerState = AudioState.Paused;
            }
            else if (QuranApp.NativeProvider.AudioProvider.State == AudioPlayerPlayState.Playing)
            {
                AudioPlayerState = AudioState.Playing;

                var track = QuranApp.NativeProvider.AudioProvider.GetTrack();
                if (track != null && track.Tag != null)
                {
                    try
                    {
                        var request = new AudioRequest(track.Tag);
                        var pageNumber = QuranUtils.GetPageFromAyah(request.CurrentAyah);
                        var oldPageIndex = CurrentPageIndex;
                        var newPageIndex = getIndexFromPageNumber(pageNumber);

                        CurrentPageIndex = newPageIndex;
                        if (oldPageIndex != newPageIndex)
                        {
                            await Task.Delay(500);
                        }
                        // If bismillah set to first ayah
                        if (request.CurrentAyah.Ayah == 0)
                            request.CurrentAyah.Ayah = 1;
                        SelectedAyah = request.CurrentAyah;
                    }
                    catch
                    {
                        // Bad track
                    }
                }
            }
        }

        #endregion
    }
}
