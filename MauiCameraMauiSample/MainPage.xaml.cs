using System.Text;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using MauiCameraMauiSample.Services;
//using MauiCameraMauiSample.Services;
using ZXing;

namespace MauiCameraMauiSample
{
    public partial class MainPage : ContentPage
    {
        public List<int> Answers { get; set; }
        AnswerManager answerManager { get; set; }
        UploadImage uploadImage { get; set; }

        public MainPage()
        {
            InitializeComponent();
            uploadImage = new UploadImage();
            answerManager = new AnswerManager();
        }

        private void cameraView_CamerasLoaded(object sender, EventArgs e)
        {
            cameraView.Camera = cameraView.Cameras.First();

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await cameraView.StopCameraAsync();
                await cameraView.StartCameraAsync();
            });
        }

        private void Btn_Take_Image(object sender, EventArgs e)
        {
            lastImage.Source = cameraView.GetSnapShot(Camera.MAUI.ImageFormat.PNG);
        }

        private async void UploadImage_Clicked(object sender, EventArgs e)
        {
            var img = await uploadImage.OpenMediaPickerAsync();
            if (img != null)
            {
                var imagefile = await uploadImage.Upload(img);

                lastImage.Source = ImageSource.FromStream(() =>
                    uploadImage.ByteArrayToStream(uploadImage.StringToByteBase64(imagefile.byteBase64))
                );
            }
        }

        private void Btn_Set_Answers(object sender, EventArgs e)
        {
            Answers = answerManager.GetAnswersFromPhoto(lastImage.Source);
            Dictionary<int, string> numberToLetter = new Dictionary<int, string>
            {
                { 0, "A" },
                { 1, "B" },
                { 2, "C" },
                { 3, "D" },
                { 4, "E" },
                { -1,"Okunamadı"}
            };
            StringBuilder result = new StringBuilder();

            foreach (int number in Answers)
            {
                if (numberToLetter.ContainsKey(number))
                {
                    if (result.Length > 0)
                    {
                        result.Append(", ");
                    }

                    result.Append(numberToLetter[number]);
                }
            }

            DisplayAlert("Cevap Anahtarı Ayarlandı", "Cevap Anahtarı: " + result, "Tamam");
        }

        private void Btn_Calculate_Score(object sender, EventArgs e)
        {
            List<int> currentAnswers = answerManager.GetAnswersFromPhoto(lastImage.Source);
            if (Answers != null && Answers.AsEnumerable().Count() == currentAnswers.AsEnumerable().Count())
            {
                double Score = MainPage.CalculateScore(Answers, currentAnswers);
                DisplayAlert("Puanlama Başarılı", "Puan: " + Score.ToString(), "Tamam");
            }
            else
            {
                DisplayAlert("Puanlama Başarısız", "Cevap anahtarı bulunamadı veya cevap şıkkı sayısı uyuşmamakta.", "Tamam");
            }
        }



        public static double CalculateScore(List<int> Answers, List<int> currentAnswers)
        {
            double correct = 0;
            for (int i = 0; i < Answers.AsEnumerable().Count(); i++)
            {
                if (Answers[i] == currentAnswers[i]) correct++;
            }
            return 100 * (correct / (double)Answers.AsEnumerable().Count());
        }
    }
}