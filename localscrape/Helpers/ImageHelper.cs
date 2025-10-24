using OpenQA.Selenium;
using System.Drawing;
using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace localscrape.Helpers
{
    public interface IImageHelper
    {
        bool AreScreenshotsSimilar(Screenshot s1, Screenshot s2, double threshold);
        bool AreScreenshotsHistogramSimilar(Bitmap reference, Screenshot s2, double threshold = 0.95);
    }

    public class ImageHelper : IImageHelper
    {
        public ImageHelper() { }

        public bool AreScreenshotsSimilar(Screenshot s1, Screenshot s2, double threshold)
        {
            using var ms1 = new MemoryStream(s1.AsByteArray);
            using var ms2 = new MemoryStream(s2.AsByteArray);

            using var bmp1 = new Bitmap(ms1);
            using var bmp2 = new Bitmap(ms2);

            if (bmp1.Width != bmp2.Width || bmp1.Height != bmp2.Height)
                return false;

            using var mat1 = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp1);
            using var mat2 = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp2);

            using var gray1 = new Mat();
            using var gray2 = new Mat();

            Cv2.CvtColor(mat1, gray1, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(mat2, gray2, ColorConversionCodes.BGR2GRAY);

            return GetStructuralSimilarity(gray1, gray2) >= threshold;
        }

        public bool AreScreenshotsHistogramSimilar(Bitmap reference, Screenshot s2, double threshold = 0.95)
        {
            using var bmp2 = new Bitmap(new MemoryStream(s2.AsByteArray));

            using var mat1 = OpenCvSharp.Extensions.BitmapConverter.ToMat(reference);
            using var mat2 = OpenCvSharp.Extensions.BitmapConverter.ToMat(bmp2);

            using var gray1 = new Mat();
            using var gray2 = new Mat();

            Cv2.CvtColor(mat1, gray1, ColorConversionCodes.BGR2GRAY);
            Cv2.CvtColor(mat2, gray2, ColorConversionCodes.BGR2GRAY);

            int[] histSize = { 256 };
            Rangef[] ranges = { new Rangef(0, 256) };
            Mat hist1 = new Mat(), hist2 = new Mat();

            Cv2.CalcHist(new[] { gray1 }, new[] { 0 }, null, hist1, 1, histSize, ranges);
            Cv2.CalcHist(new[] { gray2 }, new[] { 0 }, null, hist2, 1, histSize, ranges);

            Cv2.Normalize(hist1, hist1, 0, 1, NormTypes.MinMax);
            Cv2.Normalize(hist2, hist2, 0, 1, NormTypes.MinMax);

            double similarity = Cv2.CompareHist(hist1, hist2, HistCompMethods.Correl);
            return similarity >= threshold;
        }

        private double GetStructuralSimilarity(Mat img1, Mat img2)
        {
            Mat mu1 = new Mat(), mu2 = new Mat();
            Cv2.GaussianBlur(img1, mu1, new Size(11, 11), 1.5);
            Cv2.GaussianBlur(img2, mu2, new Size(11, 11), 1.5);

            Mat mu1Sq = mu1.Mul(mu1);
            Mat mu2Sq = mu2.Mul(mu2);
            Mat mu1Mu2 = mu1.Mul(mu2);

            Mat sigma1Sq = new Mat(), sigma2Sq = new Mat(), sigma12 = new Mat();
            Cv2.GaussianBlur(img1.Mul(img1), sigma1Sq, new Size(11, 11), 1.5);
            Cv2.GaussianBlur(img2.Mul(img2), sigma2Sq, new Size(11, 11), 1.5);
            Cv2.GaussianBlur(img1.Mul(img2), sigma12, new Size(11, 11), 1.5);

            sigma1Sq -= mu1Sq;
            sigma2Sq -= mu2Sq;
            sigma12 -= mu1Mu2;

            Scalar C1 = new Scalar(6.5025), C2 = new Scalar(58.5225);

            Mat t1 = 2 * mu1Mu2 + C1;
            Mat t2 = 2 * sigma12 + C2;
            Mat numerator = t1.Mul(t2);

            Mat t3 = mu1Sq + mu2Sq + C1;
            Mat t4 = sigma1Sq + sigma2Sq + C2;
            Mat denominator = t3.Mul(t4);

            Mat ssimMap = new Mat();
            Cv2.Divide(numerator, denominator, ssimMap);
            var mean = Cv2.Mean(ssimMap)[0];
            return mean; 
        }
    }
}
