using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using DAP.Adorners;
using System.Windows.Documents;
using System.IO;


namespace ScannerClient_obalkyknih
{
    /// <summary>
    /// Class for manipulation with images: adjusting brightness and contrast, rotation flip
    /// </summary>
    public static class ImageTools
    {
        /// <summary>
        /// Rotates image by amount of degree (only right angle rotations are allowed)
        /// </summary>
        /// <param name="image">image that will be rotated</param>
        /// <param name="degree">degree of clockwise rotation </param>
        public static BitmapSource RotateImage(BitmapSource inputSource, int degree)
        {
            RotateTransform transform = new RotateTransform(degree);
            return TransformImage(inputSource, transform);
        }

        /// <summary> Flips image horizontally </summary>
        /// <param name="inputImagePath">path to image that will be flipped</param>
        /// <param name="inputImagePath">path to file, where will be flipped image saved</param>
        public static BitmapSource FlipHorizontalImage(BitmapSource inputSource)
        {
            ScaleTransform transform = new ScaleTransform(-1, 1);
            return TransformImage(inputSource, transform);
        }

        /// <summary> Deskew the image (rotate), so its text will be straight </summary>
        /// <param name="inputImagePath">path to image that will be deskewed</param>
        /// <param name="outputImagePath">path to file, where will be deskewed image saved</param>
        /// <remarks>This is only method that uses GDI+ and does not use BitmapSource</remarks>
        public static void DeskewImage(string inputImagePath, string outputImagePath)
        {
            System.Drawing.Bitmap bmpIn = new System.Drawing.Bitmap(inputImagePath);
            Deskew sk = new Deskew(bmpIn);
            double skewangle = sk.GetSkewAngle();
            System.Drawing.Bitmap bmpOut = sk.RotateImage(bmpIn, -skewangle);
            bmpIn.Dispose();
            bmpOut.Save(outputImagePath, System.Drawing.Imaging.ImageFormat.Tiff);
            bmpOut.Dispose();
        }

        #region Cropping functions

        /// <summary>
        /// Crops image bounded by rectangle of cropper
        /// </summary>
        /// <param name="image">image that will be cropped</param>
        /// <param name="cropper">object responsible for cropping</param>
        public static BitmapSource CropImage(BitmapSource image, CroppingAdorner cropper)
        {
            if (cropper != null)
            {
                return cropper.BpsCrop(image);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds cropper object to element (adds cropping rectangle)
        /// </summary>
        /// <param name="fel">element to which will be added cropper</param>
        /// <param name="cropper">cropper, that will be added</param>
        /// <param name="cropZoneSize">width and height of cropZone</param>
        public static void AddCropToElement(FrameworkElement fel, ref CroppingAdorner cropper, Rect cropZoneSize)
        {
            AdornerLayer aly = null;
            Size cropZone = new Size(0,0);
            if (cropper != null && cropper.AdornedElement != null)
            {
                cropZone = cropper.CropZone;
                aly = AdornerLayer.GetAdornerLayer(fel);
                if (aly.GetAdorners(fel) != null)
                {
                    foreach (var adorner in aly.GetAdorners(fel))
                    {
                        aly.Remove(adorner);
                    }
                }
            }
            Rect rcInterior;
            if (cropZoneSize.Height < 1 || cropZoneSize.Width < 1)
            {
                rcInterior = new Rect(0, 0, fel.RenderSize.Width, fel.RenderSize.Height);
            }
            else
            {
                rcInterior = cropZoneSize;
            }
            aly = AdornerLayer.GetAdornerLayer(fel);
            cropper = new CroppingAdorner(fel, rcInterior);
            cropper.CropZone = cropZone;
            aly.Add(cropper);
        }
        #endregion

        /// <summary>
        /// Adjusts brightness of image, this process is irreversible
        /// </summary>
        /// <param name="sourceImage">image, that will be adjusted</param>
        /// <param name="brightness">brightness value on scale -255 to 255</param>
        /// <returns>new image with adjusted brightness</returns>
        public static BitmapSource ApplyBrightness(BitmapSource sourceImage, int brightness)
        {
            //if not BGRA transform to BGRA
            if (sourceImage.Format != PixelFormats.Bgra32)
                sourceImage = new FormatConvertedBitmap(sourceImage, PixelFormats.Bgra32, null, 0);

            int width = sourceImage.PixelWidth;
            int height = sourceImage.PixelHeight;
            int colors = sourceImage.Format.BitsPerPixel / 8;
            int stride = width * colors;

            byte[] bitmapArray = new byte[height*stride];
            sourceImage.CopyPixels(bitmapArray, stride, 0);
            
            for (int i = 0; i < height * stride; i++)
            {
                if (i % 4 != 3)
                {
                    int color = brightness + bitmapArray[i];
                    if (color > 255)
                    {
                        color = 255;
                    }
                    if (color < 0)
                    {
                        color = 0;
                    }
                    bitmapArray[i] = (byte)color;
                }
            }

            return WriteableBitmap.Create(width, height, 300, 300, PixelFormats.Bgra32, sourceImage.Palette, bitmapArray, stride);
        }

        /// <summary>
        /// Adjusts contrast of image, this process is irreversible
        /// </summary>
        /// <param name="bitmapImage">image, that will be adjusted</param>
        /// <param name="contrast">contrast level on scale -100 to 100</param>
        /// <returns>new image with adjusted contrast</returns>
        public static BitmapSource ApplyContrast(BitmapSource sourceImage, double contrast)
        {
            contrast = (100.0 + contrast) / 100.0;
            contrast *= contrast;

            //if not BGRA transform to BGRA
            if (sourceImage.Format != PixelFormats.Bgra32)
                sourceImage = new FormatConvertedBitmap(sourceImage, PixelFormats.Bgra32, null, 0);

            int width = sourceImage.PixelWidth;
            int height = sourceImage.PixelHeight;
            int colors = sourceImage.Format.BitsPerPixel / 8;
            int stride = width * colors;

            byte[] bitmapArray = new byte[height * stride];
            sourceImage.CopyPixels(bitmapArray, stride, 0);
            
            for (int i = 0; i < height * stride; i++)
            {
                if (i % 4 != 3)
                {
                    double tmpContrast = bitmapArray[i] / 255.0;
                    tmpContrast -= 0.5;
                    tmpContrast *= contrast;
                    tmpContrast += 0.5;
                    tmpContrast *= 255;

                    if (tmpContrast > 255)
                    {
                        tmpContrast = 255;
                    }
                    else if (tmpContrast < 0)
                    {
                        tmpContrast = 0;
                    }
                    bitmapArray[i] = (byte)tmpContrast;
                }
            }

            return WriteableBitmap.Create(width,height,sourceImage.DpiX, sourceImage.DpiY, sourceImage.Format, sourceImage.Palette, bitmapArray, stride);
        }

        // Applies transformation to inputSource and returned transformed BitmapSource
        private static BitmapSource TransformImage(BitmapSource inputSource, Transform transform)
        {
            TransformedBitmap tb = new TransformedBitmap();
            tb.BeginInit();
            tb.Source = inputSource;
            tb.Transform = transform;
            tb.EndInit();

            return tb;
        }

        /// <summary>
        /// Saves BitmapSource into file with with given path
        /// </summary>
        /// <param name="source">BitmapSource that will be saved</param>
        /// <param name="outputFile">Absolute path to file, where image will be saved</param>
        public static void SaveToFile(BitmapSource source, string outputFile)
        {
            using (FileStream fs = new FileStream(outputFile, FileMode.Create))
            {
                TiffBitmapEncoder encoder = new TiffBitmapEncoder();
                encoder.Compression = TiffCompressOption.Lzw;
                encoder.Frames.Add(BitmapFrame.Create(source));
                encoder.Save(fs);
            }
        }

        /// <summary>
        /// Loads BitmapImage with given decode width from file with with given path
        /// </summary>
        /// <param name="fileName">File path to image</param>
        /// <param name="decodePixelHeight">Pixel height of decoded image</param>
        /// <returns>Decoded BitmapImage</returns>
        public static BitmapImage LoadGivenSizeFromFile(string fileName, int? decodePixelHeight)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(fileName);
            if (decodePixelHeight != null && decodePixelHeight > 0)
            {
                bitmap.DecodePixelHeight = (int)decodePixelHeight;
            }
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            return bitmap;
        }
        /// <summary>
        /// Loads full BitmapImage from file with with given path
        /// </summary>
        /// <param name="fileName">File path to image</param>
        /// <returns>Decoded BitmapImage</returns>
        public static BitmapImage LoadFullSizeFromFile(string fileName)
        {
            return LoadGivenSizeFromFile(fileName, null);
        }
    }
}
