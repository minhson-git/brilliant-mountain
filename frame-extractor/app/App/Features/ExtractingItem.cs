using FFMpegCore;
using IOCore.Base;
using IOCore.Core;
using IOCore.Files;
using IOCore.Gens;
using IOCore.Helpers;
using IOCore.Utils;
using IOImage;
using IOMedia.Media;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static IOApp.Configs.AppTypes;

namespace IOApp.Features
{
    internal partial class ExtractingItem(MediaInfoBase mediaInfoBase) : MediaItem(mediaInfoBase)
    {
        #region Frame interval

        public int FrameInterval { get; set => SetAndNotify(ref field, value); } = 0;

        #endregion

        public ExtractingConvertArgv ExtractingConvertArgv { get; } = new();

        public ExtractingItem(string path) : this(MediaInfoBase.Create(path))
        {
            ExtractingConvertArgv.IntervalTypes.Replace(IntervalTypeArray.Select(i => new OptionItem<IntervalType>(i, R.T(INTERVAL_TYPES[i]))));
            ExtractingConvertArgv.IntervalIndex = 0;
        }

        public async Task<int> ExtractFrames(string outputFolderPath)
        {
            if (InputInfo.Media is null)
            {
                InputInfo.Analyze();

                if (InputInfo.Media is null)
                    throw new Exception();
            }

            var extractedCount = 0;

            IProgress<double> progress = new Progress<double>(percent => Percent = (int)percent);

            switch (ExtractingConvertArgv.IntervalType)
            {
                case IntervalType.Frames:
                    var frameRate = double.TryParse(InputInfo.Media.FrameRate, out var parsedFrameRate) ? parsedFrameRate : 30;

                    await FFMpegArguments
                        .FromFileInput(InputInfo.FullName)
                        .OutputToFile(Path.Combine(outputFolderPath, "frame_%d.png"), true,
                            options => options
                                .WithVideoCodec("png")
                                .WithCustomArgument($"-q:v 2 -vf \"select=not(mod(n\\,{FrameInterval}))\" -vsync vfr")
                                .WithFrameOutputCount(ExtractingConvertArgv.NumberOfFramesToExtract))
                        .NotifyOnProgress(percent => progress.Report(percent / 2.0), InputInfo.Media.Duration)
                        .ProcessAsynchronously();

                    extractedCount = ExtractingConvertArgv.NumberOfFramesToExtract;
                    break;

                case IntervalType.Time:
                    var startTimeSpan = new TimeSpan(
                        ExtractingConvertArgv.TimeIntervalHour,
                        ExtractingConvertArgv.TimeIntervalMinute,
                        ExtractingConvertArgv.TimeIntervalSecond
                    ).Add(TimeSpan.FromMilliseconds(ExtractingConvertArgv.TimeIntervalMilisecond));

                    var startAtSeconds = startTimeSpan.TotalSeconds;

                    if (startAtSeconds < 0 || startAtSeconds >= InputInfo.Media.Duration.TotalSeconds)
                        throw new ArgumentException(string.Format(
                            R.T(L.StartTimeExceedsDuration),
                            startAtSeconds,
                            InputInfo.Media.Duration.TotalSeconds
                        ));
                       
                    var frameIntervalMs = ExtractingConvertArgv.FrameIntervalMs;
                    if (frameIntervalMs <= 0)
                        throw new ArgumentException(R.T(L.TimeIntervalGreaterThanZero));

                    var frameIntervalSeconds = frameIntervalMs / 1000.0;

                    var totalAvailableSeconds = InputInfo.Media.Duration.TotalSeconds - startAtSeconds;
                    if (totalAvailableSeconds <= 0)
                        throw new ArgumentException(string.Format(R.T(L.StartTimeExceedsDuration), startAtSeconds, InputInfo.Media.Duration.TotalSeconds));

                    var maxExtractableFrames = (int)(totalAvailableSeconds / frameIntervalSeconds);
                    var actualFramesToExtract = Math.Min(ExtractingConvertArgv.NumberOfFramesToExtract, maxExtractableFrames);

                    if (actualFramesToExtract <= 0)
                        throw new ArgumentException(string.Format(R.T(L.TimeIntervalTooLargeForDuration), frameIntervalSeconds, totalAvailableSeconds));

                    var fps = 1.0 / frameIntervalSeconds;
                    var fpsArg = fps.ToString("0.########", CultureInfo.InvariantCulture);

                    var vf = $"fps=fps={fpsArg}:start_time={startAtSeconds.ToString(CultureInfo.InvariantCulture)}:round=up";

                    var outputFilePath = Path.Combine(outputFolderPath, "frame_%d.png");

                    await FFMpegArguments
                        .FromFileInput(InputInfo.FullName)
                        .OutputToFile(
                            outputFilePath,
                            true,
                            options => options
                                .Seek(TimeSpan.FromSeconds(startAtSeconds))
                                .WithFrameOutputCount(actualFramesToExtract)
                                .WithVideoCodec("png")
                                .WithCustomArgument($"-vf \"{vf}\" -q:v 2 -start_number 1")
                        )
                        .NotifyOnProgress(
                            percent => progress.Report(Percent + percent / 2.0),
                            InputInfo.Media.Duration
                        )
                        .ProcessAsynchronously();

                    extractedCount = actualFramesToExtract;
                    break;
            }

            return extractedCount;
        }

        public async Task Process(string outputFolderPath)
        {
            string? extractedFramesDirPath = null;
            string? convertedFramesDirPath = null;

            try
            {
                Status.SetAndNotify(S.Processing);

                var outputDirName = InputInfo.NameWithoutExtension;

                extractedFramesDirPath = AppDir.LGet(AppDir.Type.TemporaryFolder, FormatUtils.UID);
                FileUtils.CreateDirectoryIfNotExist(extractedFramesDirPath);

                var extractedCount = await ExtractFrames(extractedFramesDirPath);
                if (extractedCount <= 0)
                    throw new Exception(R.T(L.NoFramesExtracted));

                var framePaths = Directory.EnumerateFiles(extractedFramesDirPath).ToArray();
                if (framePaths.Length == 0)
                    throw new Exception(string.Format(R.T(L.ExtractedCountReportButNoFrames), extractedCount));

                convertedFramesDirPath = AppDir.LGet(AppDir.Type.TemporaryFolder, FormatUtils.UID);
                FileUtils.CreateDirectoryIfNotExist(convertedFramesDirPath);

                foreach (var framePath in framePaths)
                {
                    var imageItem = new ImageItem(framePath);
                    imageItem.InputInfo.Analyze();
                    imageItem.ConvertArgv.Copy(ExtractingConvertArgv);

                    await imageItem.Convert(convertedFramesDirPath, false, null);
                }

                var outputPath = ExtractingConvertArgv.DoCompression ?
                    Path.Combine(outputFolderPath, $"{outputDirName}.zip") : 
                    Path.Combine(outputFolderPath, outputDirName);

                if (!ExtractingConvertArgv.Overwrite)
                outputPath = PathUtils.NextAvailablePath(outputPath);

                if (ExtractingConvertArgv.DoCompression)
                    ArchiveUtils.CreateZip(convertedFramesDirPath, outputPath);
                else
                    FileUtils.CopyDirectory(convertedFramesDirPath, outputPath, true);

                OutputInfo.Refresh(outputPath, true);

                Status.SetAndNotify(S.Processed);
            }
            catch (OperationCanceledException)
            {
                Status.SetAndNotify(S.ProcessStopped);
            }
            catch (Exception)
            {
                Status.SetAndNotify(S.ProcessFailed);
            }
            finally
            {
                FileUtils.DeleteDirectory(extractedFramesDirPath, true);
                FileUtils.DeleteDirectory(convertedFramesDirPath, true);
            }
        }
    }
}
