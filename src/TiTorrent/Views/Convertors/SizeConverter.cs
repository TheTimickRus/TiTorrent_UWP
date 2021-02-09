﻿using System;
using Windows.UI.Xaml.Data;

namespace TiTorrent.Views
{
    public class SizeConverter : IValueConverter
    {
        private static string ConvertComputerValues(dynamic value, int param = 0)
        {
            string[,] strings =
            {
                {"Byte", "KB", "MB", "GB", "TB", "PB", "EB"},
                {"Byte/s", "KB/s", "MB/s", "GB/s", "TB/s", "PB/s", "EB/s"}
            };

            if (value == 0)
            {
                return $"0 {strings[param, 0]}";
            }

            var bytes = Math.Abs(value);
            var place = System.Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);

            return $"{Math.Sign(value) * num} {strings[param, place]}";
        }

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value == null 
                ? null 
                : ConvertComputerValues(value, int.Parse(parameter.ToString()));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
