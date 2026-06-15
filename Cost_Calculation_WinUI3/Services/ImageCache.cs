using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Cost_Calculation.Services
{
    /// <summary>
    /// Кеш декодированных изображений по URI: BitmapImage создаётся один раз и
    /// переиспользуется, поэтому повторное построение карточек (агенты, иконки
    /// сетов) не перечитывает и не декодирует файлы заново. Только UI-поток.
    /// </summary>
    public static class ImageCache
    {
        private static readonly Dictionary<string, BitmapImage?> _cache = new();

        public static BitmapImage? Get(string? uri)
        {
            if (string.IsNullOrEmpty(uri)) return null;
            if (_cache.TryGetValue(uri, out var cached)) return cached;

            BitmapImage? image = null;
            try
            {
                image = new BitmapImage(new Uri(uri));
            }
            catch (Exception ex)
            {
                Logger.Error($"ImageCache load failed for {uri}", ex);
            }

            _cache[uri] = image;
            return image;
        }
    }
}
