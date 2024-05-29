using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Emgu.CV;
using System.Drawing;
using Bitmap = System.Drawing.Bitmap;
using ImageSource = Microsoft.Maui.Controls.ImageSource;
using System.Drawing.Imaging;

namespace MauiCameraMauiSample.Services
{
    public class AnswerManager
    {
        public List<int> GetAnswersFromPhoto(ImageSource imageSource)
        {
            List<int> result = new List<int>();

            //string path = "C:\\omr4.png";
            //int widthImg = 1280;
            //int heightImg = 720;
            //Size size = new Size(widthImg, heightImg);
            //Mat img = new Mat();
            //using (Stream stream = ((StreamImageSource)image).Stream(CancellationToken.None).Result)
            //{
            //    using (MemoryStream memoryStream = new MemoryStream())
            //    {
            //        stream.CopyTo(memoryStream);
            //        byte[] imageData = memoryStream.ToArray();

            //        GCHandle handle = GCHandle.Alloc(imageData, GCHandleType.Pinned);
            //        IntPtr ptr = handle.AddrOfPinnedObject();

            //        img = new Mat(imageData.Length / imageData[0], 1, DepthType.Cv8U, (int)ptr);
            //    }
            //}


            var streamImageSource = (StreamImageSource)imageSource;
            Stream stream = streamImageSource.Stream(CancellationToken.None).Result; // Senkron hale getiriyoruz

            Bitmap bitmap;
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin); // Başlangıca dön
                bitmap = new Bitmap(memoryStream);
            }

            Mat img = new Mat(bitmap.Height, bitmap.Width, DepthType.Cv8U, 3); // 3 kanal (BGR) olduğunu varsayıyoruz
            BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            int width = bitmapData.Width * 3; // 3 byte per pixel for 24bppRgb
            byte[] data = new byte[bitmapData.Stride * bitmapData.Height];
            System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, data, 0, data.Length);

            for (int y = 0; y < bitmapData.Height; y++)
            {
                IntPtr sourcePtr = IntPtr.Add(bitmapData.Scan0, y * bitmapData.Stride);
                IntPtr destPtr = IntPtr.Add(img.DataPointer, y * img.Step);
                System.Runtime.InteropServices.Marshal.Copy(data, y * bitmapData.Stride, destPtr, width);
            }

            bitmap.UnlockBits(bitmapData);



            int widthImg = img.Width;
            int heightImg = img.Height;

            CvInvoke.Imshow("resizedOrg", img);
            CvInvoke.WaitKey(0);


            Mat? imgGray = new();
            CvInvoke.CvtColor(img, imgGray, ColorConversion.Bgr2Gray);

            ////CvInvoke.Imshow("imgGray", imgGray);
            ////CvInvoke.WaitKey(0);

            Mat? imgBlur = new();
            CvInvoke.GaussianBlur(imgGray, imgBlur, new System.Drawing.Size(5, 5), 0);

            //CvInvoke.Imshow("imgBlur", imgBlur);
            //CvInvoke.WaitKey(0);

            Mat? imgCanny = new();
            CvInvoke.Canny(imgBlur, imgCanny, 75, 200);

            CvInvoke.Imshow("imgCanny", imgCanny);
            CvInvoke.WaitKey(0);

            //Mat? imgCannyCopy = new(); ;
            //imgCanny.CopyTo(imgCannyCopy);

            VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint();
            Mat hierarchy = new Mat();

            CvInvoke.FindContours(imgCanny, contours, hierarchy, mode: RetrType.External, method: ChainApproxMethod.ChainApproxSimple);

            for (int i = 0; i < contours.Size; i++)
            { 
            CvInvoke.DrawContours(img, contours, i, new MCvScalar(255, 0, 0), 1);
            }


            CvInvoke.Imshow("imgContour", img);
            CvInvoke.WaitKey(0);


            VectorOfPoint docCnt = null;
            double docCntArea = 0;

            if (contours.Size > 0)
            {
                for (int i = 0; i < contours.Size; i++)
                {
                    var a = CvInvoke.ContourArea(contours[i]);


                    var peri = CvInvoke.ArcLength(contours[i], true);
                    VectorOfPoint approx = new VectorOfPoint();
                    CvInvoke.ApproxPolyDP(contours[i], approx, 0.02 * peri, true);
                    
                    if (approx.Size == 4)
                    {
                        var area = CvInvoke.ContourArea(contours[i]);

                        if (area > docCntArea)
                        {
                            docCntArea = area;
                        docCnt = approx;
                        }
                        //break;
                    }
                }
            }           

            docCnt = docCnt ?? new VectorOfPoint(new System.Drawing.Point[] {
                                                        new System.Drawing.Point(0, 0),
                                                        new System.Drawing.Point(0, heightImg),
                                                        new System.Drawing.Point(widthImg, heightImg),
                                                        new System.Drawing.Point(widthImg, 0)
            });
            System.Drawing.PointF[] srcPoints = docCnt.ToArray().Select(p => new System.Drawing.PointF(p.X, p.Y)).ToArray();

            System.Drawing.PointF[] dstPoints = new System.Drawing.PointF[] {
                                                        new System.Drawing.PointF(0, 0),
                                                        new System.Drawing.PointF(0, heightImg),
                                                        new System.Drawing.PointF(widthImg, heightImg),
                                                        new System.Drawing.PointF(widthImg, 0)
            };

            System.Drawing.PointF[] dstPoints2 = new System.Drawing.PointF[srcPoints.Length];
            Mat paper = new Mat();
            Mat warped = new Mat();
            // Perspektif dönüşümü matrisini oluşturun
            Mat matrix = CvInvoke.GetPerspectiveTransform(srcPoints, dstPoints);
            CvInvoke.WarpPerspective(img, paper, matrix, new System.Drawing.Size(widthImg, heightImg));
            CvInvoke.WarpPerspective(imgGray, warped, matrix, new System.Drawing.Size(widthImg, heightImg));



            Mat thresh = new();
            CvInvoke.Threshold(warped, thresh, 0, 255, ThresholdType.BinaryInv | ThresholdType.Otsu);
            CvInvoke.Imshow("threshold", thresh);
            CvInvoke.WaitKey(0);

            VectorOfVectorOfPoint contours2 = new VectorOfVectorOfPoint();
            Mat hierarchy2 = new Mat();
            Mat? imgThreshCopy = new(); ;
            thresh.CopyTo(imgThreshCopy);

            CvInvoke.FindContours(imgThreshCopy, contours2, hierarchy2, mode: RetrType.External, method: ChainApproxMethod.ChainApproxSimple);

            //CvInvoke.DrawContours(paper, contours2, 6, new MCvScalar(255, 0, 0), 20);
            //CvInvoke.DrawContours(paper, contours2, 7, new MCvScalar(255, 0, 0), 20);

            //for (int i = 0; i < contours2.Size; i++)
            //{
            //    CvInvoke.DrawContours(paper, contours2, i, new MCvScalar(255, 0, 0), 5);

            //}

            //CvInvoke.Imshow("imgContour", paper);
            //CvInvoke.WaitKey(0);

            VectorOfVectorOfPoint questionCnts = new VectorOfVectorOfPoint();

            //var totalWidthHeight = 0;
            //for (int i = 0; i < contours2.Size; i++)
            //{
            //    Rectangle rect = CvInvoke.BoundingRectangle(contours2[i]);
            //    totalWidthHeight = rect.Width + rect.Height;
            //}
            //var averageDiameter = totalWidthHeight / (contours2.Size * 2);

            for (int i = 0; i < contours2.Size; i++)
            {
                Rectangle rect = CvInvoke.BoundingRectangle(contours2[i]);

                if (rect.Width < 10 || rect.Height < 10)
                {
                    continue;
                }

                float aspectRatio = (float)rect.Width / (float)rect.Height;
                if (aspectRatio >= 0.9 && aspectRatio <= 1.1)
                //   if (true)                    
                {
                    questionCnts.Push(contours2[i]);
                }
            }

            for (int i = 0; i < questionCnts.Size; i++)
            {
                CvInvoke.DrawContours(paper, questionCnts, i, new MCvScalar(255, 0, 0), 2);

            }

            var aaaaa = questionCnts.ToArrayOfArray().OrderByDescending(a => a.Min(b => b.Y));
            System.Drawing.Point[][] points = new System.Drawing.Point[aaaaa.Count()][];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = aaaaa.ToList()[i];
            }
            questionCnts = new VectorOfVectorOfPoint(points);

            Mat threshOutput = new();

            int isaretAlaniSayisi = 5;

            for (int i = 0; i < questionCnts.Size; i = i + isaretAlaniSayisi)
            {
                points = new System.Drawing.Point[isaretAlaniSayisi][];
                for (int j = 0; j < isaretAlaniSayisi; j++)
                {
                    points[j] = aaaaa.ToList()[i + j];
                }
                points = points.OrderBy(a => a.Min(b => b.X)).ToArray();
                VectorOfVectorOfPoint soruCevapAlani = new VectorOfVectorOfPoint(points);

                List<int> pikselSayileri = new List<int>();

                for (int j = 0; j < soruCevapAlani.Size; j++)
                {
                    using (VectorOfPoint c = soruCevapAlani[j])
                    {
                        // Construct a mask to reveal only the current "bubble" for the question
                        Mat mask = new Mat(thresh.Size, DepthType.Cv8U, 1);
                        mask.SetTo(new MCvScalar(0));
                        CvInvoke.DrawContours(mask, new VectorOfVectorOfPoint(new VectorOfPoint[] { c }), -1, new MCvScalar(255), -1);

                        // Apply the mask to the thresholded image
                        Mat maskedThreshold = new Mat();
                        CvInvoke.BitwiseAnd(thresh, thresh, maskedThreshold, mask);

                        CvInvoke.Imshow("threshold", maskedThreshold);
                        CvInvoke.WaitKey(0);

                        // Count the number of non-zero pixels in the bubble area
                        int total = CvInvoke.CountNonZero(maskedThreshold);
                        pikselSayileri.Add(total);
                    }
                }

                var maxIndex = pikselSayileri.IndexOf(pikselSayileri.Max());
                var maxValue = pikselSayileri.Max();
                pikselSayileri.RemoveAt(maxIndex);

                if (pikselSayileri.All(a => maxValue > a * 1.9))
                {
                result.Add(maxIndex);
                }
                else
                { 
                result.Add(-1);
                }

            }

            result.Reverse();

            return result;
        }
    }
}
