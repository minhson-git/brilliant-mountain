using IOCore.Base;
using IOCore.Exs;
using IOImage.ConverterBase;
using System;
using static IOApp.Configs.AppTypes;

namespace IOApp.Features
{
    partial class ExtractingConvertArgv : ConvertArgv
    {
        public ObservableCollectionEx<OptionItem<IntervalType>> IntervalTypes { get; } = [];
        public int IntervalIndex
        {
            get;
            set
            {
                SetAndNotify(ref field, value);
                Notify(nameof(IntervalType));
            }
        } = -1;
        public IntervalType IntervalType => IntervalTypes.GetAtOrDefault(IntervalIndex, IntervalType.Time);

        public bool Overwrite { get; set => SetAndNotify(ref field, value); } = false;

        public bool DoCompression { get; set => SetAndNotify(ref field, value); } = false;

        public int FrameIntervalMs { get; set => SetAndNotify(ref field, value); } = 10;

        public int NumberOfFramesToExtract { get; set => SetAndNotify(ref field, value); } = 0;

        #region Time interval

        public string TimeIntervalHourStr
        {
            get;
            set
            {
                if (int.TryParse(value, out var result))
                    result = Math.Abs(result);
                else
                    result = 0;

                SetAndNotify(ref field, result.ToString());
            }
        } = "0";
        public int TimeIntervalHour => int.Parse(TimeIntervalHourStr);

        public string TimeIntervalMinuteStr
        {
            get;
            set
            {
                if (int.TryParse(value, out var result))
                    result = Math.Clamp(result, 0, 59);
                else
                    result = 0;

                SetAndNotify(ref field, result.ToString());
            }
        } = "0";
        public int TimeIntervalMinute => int.Parse(TimeIntervalMinuteStr);

        public string TimeIntervalSecondStr
        {
            get;
            set
            {
                if (int.TryParse(value, out var result))
                    result = Math.Clamp(result, 0, 59);
                else
                    result = 0;

                SetAndNotify(ref field, result.ToString());
            }
        } = "0";
        public int TimeIntervalSecond => int.Parse(TimeIntervalSecondStr);

        public string TimeIntervalMilisecondStr
        {
            get;
            set
            {
                if (int.TryParse(value, out var result))
                    result = Math.Clamp(result, 0, 999);
                else
                    result = 0;

                SetAndNotify(ref field, result.ToString());
            }
        } = "0";
        public int TimeIntervalMilisecond => int.Parse(TimeIntervalMilisecondStr);

        #endregion

        public ExtractingConvertArgv() : base()
        {

        }

        public void CopyExtractingConvertArgv(ExtractingConvertArgv? argv, bool includeFamily = true)
        {
            if(argv is null || argv == this)
                return;

            if(argv is ExtractingConvertArgv extractingConvertArgv)
            {
                IntervalIndex = extractingConvertArgv.IntervalIndex;
                Overwrite = extractingConvertArgv.Overwrite;
                DoCompression = extractingConvertArgv.DoCompression;
                FrameIntervalMs = extractingConvertArgv.FrameIntervalMs;
                NumberOfFramesToExtract = extractingConvertArgv.NumberOfFramesToExtract;

                TimeIntervalHourStr = extractingConvertArgv.TimeIntervalHourStr;
                TimeIntervalMinuteStr = extractingConvertArgv.TimeIntervalMinuteStr;
                TimeIntervalSecondStr = extractingConvertArgv.TimeIntervalSecondStr;
                TimeIntervalMilisecondStr = extractingConvertArgv.TimeIntervalMilisecondStr;
            }

            Copy(argv, includeFamily);
        }
    }
}
