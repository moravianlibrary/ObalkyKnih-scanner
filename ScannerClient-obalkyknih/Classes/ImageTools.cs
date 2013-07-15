using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using DAP.Adorners;
using System.Windows.Documents;


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
        public static void RotateImage(Image image, int degree)
        {
            TransformedBitmap bi = new TransformedBitmap();
            bi.BeginInit();
            bi.Source = image.Source as BitmapSource;
            RotateTransform transform = new RotateTransform(degree);
            bi.Transform = transform;
            bi.EndInit();
            image.Source = bi;
        }

        /// <summary>
        /// Flips image vertically
        /// </summary>
        /// <param name="image">image that will be flipped</param>
        public static void FlipVerticalImage(Image image)
        {
            TransformedBitmap bi = new TransformedBitmap();
            bi.BeginInit();
            bi.Source = image.Source as BitmapSource;
            ScaleTransform transform = new ScaleTransform(1, -1);
            bi.Transform = transform;
            bi.EndInit();
            image.Source = bi;
        }

        /// <summary>
        /// Flips image horizontally (mirror image)
        /// </summary>
        /// <param name="image">image that will be flipped</param>
        public static void FlipHorizontalImage(Image image)
        {
            TransformedBitmap bi = new TransformedBitmap();
            bi.BeginInit();
            bi.Source = image.Source as BitmapSource;
            ScaleTransform transform = new ScaleTransform(-1, 1);
            bi.Transform = transform;
            bi.EndInit();
            image.Source = bi;
        }

        #region Cropping functions

        /// <summary>
        /// Crops image bounded by rectangle of cropper
        /// </summary>
        /// <param name="image">image that will be cropped</param>
        /// <param name="cropper">object responsible for cropping</param>
        public static void CropImage(Image image, ref CroppingAdorner cropper)
        {
            if (cropper != null)
            {
                Rect rc = cropper.ClippingRectangle;
                image.Source = cropper.BpsCrop();
                // reset cropping zone to full image again
                AddCropToElement(image, ref cropper);
            }
        }

        /// <summary>
        /// Removes cropper object from the element (removes the cropping rectangle)
        /// </summary>
        /// <param name="cropper"></param>
        public static void RemoveCropFromElement(CroppingAdorner cropper)
        {
            var element = cropper.AdornedElement;
            AdornerLayer aly = AdornerLayer.GetAdornerLayer(element);
            aly.Remove(cropper);
        }

        /// <summary>
        /// Adds cropper object to element (adds cropping rectangle)
        /// </summary>
        /// <param name="fel">element to which will be added cropper</param>
        /// <param name="cropper">cropper, that will be added</param>
        public static void AddCropToElement(FrameworkElement fel, ref CroppingAdorner cropper)
        {
            if (cropper != null && cropper.AdornedElement != null)
            {
                RemoveCropFromElement(cropper);
            }
            fel.InvalidateArrange();
            fel.UpdateLayout();
            Rect rcInterior = new Rect(0, 0, fel.RenderSize.Width, fel.RenderSize.Height/*fel.ActualWidth, fel.ActualHeight*/);
            AdornerLayer aly = AdornerLayer.GetAdornerLayer(fel);
            cropper = new CroppingAdorner(fel, rcInterior);
            aly.Add(cropper);
        }
        #endregion

        /// <summary>
        /// Adjusts brightness of image, this process is irreversible
        /// </summary>
        /// <param name="bitmapImage">image, that will be adjusted</param>
        /// <param name="brightness">brightness value on scale -255 to 255</param>
        /// <returns>new image with adjusted brightness</returns>
        public static BitmapSource ApplyBrightness(BitmapSource bitmapImage, int brightness)
        {
            int width = bitmapImage.PixelWidth;
            int height = bitmapImage.PixelHeight;
            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 300, 300, PixelFormats.Bgra32, null);
            double A, R, G, B;

            PixelColor pixelColor;
            PixelColor[,] bitmap = GetPixels(bitmapImage);

            for (int y = 0; y < bitmap.GetLength(1); y++)
            {
                for (int x = 0; x < bitmap.GetLength(0); x++)
                {
                    pixelColor = bitmap[x, y];
                    A = pixelColor.Alpha;
                    R = pixelColor.Red + brightness;
                    if (R > 255)
                    {
                        R = 255;
                    }
                    else if (R < 0)
                    {
                        R = 0;
                    }

                    G = pixelColor.Green + brightness;
                    if (G > 255)
                    {
                        G = 255;
                    }
                    else if (G < 0)
                    {
                        G = 0;
                    }

                    B = pixelColor.Blue + brightness;
                    if (B > 255)
                    {
                        B = 255;
                    }
                    else if (B < 0)
                    {
                        B = 0;
                    }

                    //Set the value
                    pixelColor.Alpha = (byte)A;
                    pixelColor.Blue = (byte)B;
                    pixelColor.Green = (byte)G;
                    pixelColor.Red = (byte)R;
                    bitmap[x, y] = pixelColor;
                }
            }
            //save bitmap back to image
            byte[] result = PixelColorToByteArray(width, height, bitmap);
            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), result, width * 4, 0);
            return writeableBitmap;
        }

        /// <summary>
        /// Adjusts contrast of image, this process is irreversible
        /// </summary>
        /// <param name="bitmapImage">image, that will be adjusted</param>
        /// <param name="contrast">contrast level on scale -100 to 100</param>
        /// <returns>new image with adjusted contrast</returns>
        public static BitmapSource ApplyContrast(BitmapSource bitmapImage, double contrast)
        {
            int width = bitmapImage.PixelWidth;
            int height = bitmapImage.PixelHeight;
            WriteableBitmap writeableBitmap = new WriteableBitmap(width, height, 300, 300, PixelFormats.Bgra32, null);
            //writeableBitmap.WritePixels();
            double A, R, G, B;

            PixelColor pixelColor;

            contrast = (100.0 + contrast) / 100.0;
            contrast *= contrast;
            PixelColor[,] bitmap = GetPixels(bitmapImage);

            for (int y = 0; y < bitmap.GetLength(1); y++)
            {
                for (int x = 0; x < bitmap.GetLength(0); x++)
                {
                    pixelColor = bitmap[x, y];
                    A = pixelColor.Alpha;

                    R = pixelColor.Red / 255.0;
                    R -= 0.5;
                    R *= contrast;
                    R += 0.5;
                    R *= 255;

                    if (R > 255)
                    {
                        R = 255;
                    }
                    else if (R < 0)
                    {
                        R = 0;
                    }

                    G = pixelColor.Green / 255.0;
                    G -= 0.5;
                    G *= contrast;
                    G += 0.5;
                    G *= 255;
                    if (G > 255)
                    {
                        G = 255;
                    }
                    else if (G < 0)
                    {
                        G = 0;
                    }

                    B = pixelColor.Blue / 255.0;
                    B -= 0.5;
                    B *= contrast;
                    B += 0.5;
                    B *= 255;
                    if (B > 255)
                    {
                        B = 255;
                    }
                    else if (B < 0)
                    {
                        B = 0;
                    }

                    //Set the value
                    pixelColor.Alpha = (byte)A;
                    pixelColor.Blue = (byte)B;
                    pixelColor.Green = (byte)G;
                    pixelColor.Red = (byte)R;
                    bitmap[x, y] = pixelColor;
                }
            }

            //save bitmap back to image
            byte[] result = PixelColorToByteArray(width, height, bitmap);
            writeableBitmap.WritePixels(new Int32Rect(0, 0, width, height), result, width * 4, 0);
            return writeableBitmap;
        }

        // Creates 2-dimensional array of colored pixels from source picture
        private static PixelColor[,] GetPixels(BitmapSource source)
        {
            if (source.Format != PixelFormats.Bgra32)
                source = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

            int width = source.PixelWidth;
            int height = source.PixelHeight;
            int stride = width * 4;
            byte[] byteArray = new byte[stride * height];

            source.CopyPixels(byteArray, stride, 0);

            return ByteArrayToPixelColor(width, height, byteArray);
        }

        // Creates 2-dimensional array of colored pixels from 1 dimensional array,
        // where each pixel has 4 bytes of color channels
        private static PixelColor[,] ByteArrayToPixelColor(int width, int height, byte[] byteArray)
        {
            PixelColor[,] result = new PixelColor[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    result[x, y] = new PixelColor
                    {
                        Blue = byteArray[(y * width + x) * 4 + 0],
                        Green = byteArray[(y * width + x) * 4 + 1],
                        Red = byteArray[(y * width + x) * 4 + 2],
                        Alpha = byteArray[(y * width + x) * 4 + 3],
                    };
            return result;
        }

        // Creates bitmap of pixel with 4 channel of color per pixel from PixelColor 2d array
        private static byte[] PixelColorToByteArray(int width, int height, PixelColor[,] pixelColor)
        {
            byte[] result = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    result[(x + y * width) * 4 + 0] = pixelColor[x, y].Blue;
                    result[(x + y * width) * 4 + 1] = pixelColor[x, y].Green;
                    result[(x + y * width) * 4 + 2] = pixelColor[x, y].Red;
                    result[(x + y * width) * 4 + 3] = pixelColor[x, y].Alpha;
                };
            return result;
        }
    }

    /// <summary>
    /// Represents structure of RGBA image encoding
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PixelColor
    {
        public byte Blue;
        public byte Green;
        public byte Red;
        public byte Alpha;
    }
}
