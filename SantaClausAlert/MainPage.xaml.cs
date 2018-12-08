using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.AI.MachineLearning;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Toolkit.Uwp.Helpers;
using SantaClausAlert.Model;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SantaClausAlert
{
    /// <summary>
    ///     An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private bool elaborate;
        private DateTime lastAlert = DateTime.MinValue;
        private modelModel ModelGen;
        private readonly modelInput ModelInput = new modelInput();
        private modelOutput ModelOutput;

        private readonly ObservableCollection<ResultModel> resultsList = new ObservableCollection<ResultModel>();

        public MainPage()
        {
            InitializeComponent();
            ListViewResults.ItemsSource = resultsList;
            LoadModelAsync();
        }

        private async void CameraPreviewControl_FrameArrived(object sender, FrameEventArgs e)

        {
            if (elaborate || DateTime.Now.Subtract(lastAlert).TotalSeconds < 1)
                return;
            var videoFrame = e.VideoFrame;

            var softwareBitmap = e.VideoFrame.SoftwareBitmap;

            var targetSoftwareBitmap = softwareBitmap;


            if (softwareBitmap != null)

            {
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    softwareBitmap.BitmapAlphaMode == BitmapAlphaMode.Straight)
                    targetSoftwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied);


                var inputImage = VideoFrame.CreateWithSoftwareBitmap(targetSoftwareBitmap);

                EvaluateVideoFrameAsync(inputImage);

                //await softwareBitmapSource.SetBitmapAsync(targetSoftwareBitmap);
            }
        }

        private async void EvaluateVideoFrameAsync(VideoFrame inputImage)
        {
            elaborate = true;
            ModelInput.data = ImageFeatureValue.CreateFromVideoFrame(await CenterCropImageAsync(inputImage, 227, 227));

            //Evaluate the model
            ModelOutput = await ModelGen.EvaluateAsync(ModelInput);
            var res = ModelOutput.ClassLabel.GetAsVectorView().ToList();
            if (ModelOutput.Loss == null || ModelOutput.Loss.Count == 0)
                return;
            var loss = ModelOutput.Loss.ToList()[0];

            var maxValue = loss.Values.Max();
            if (maxValue > 0.7)
            {
                lastAlert = DateTime.Now;

                var pos = loss.Values.ToList().IndexOf(maxValue);
                var label = loss.Keys.ToList().ElementAt(pos);

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                    () =>

                    {

                        
                        //Get current image

                        var m = new Image();

                        var source = new SoftwareBitmapSource();

                        source.SetBitmapAsync(inputImage.SoftwareBitmap);

                        //m.Source = source;


                        var lossStr = new ResultModel
                        {
                            Name = label,

                            Percent = maxValue * 100.0f,

                            Image = source
                        };

                        //loss.Select(l => new ResultModel()

                        //{

                        //    Name = l.Key,

                        //    Percent = l.Value * 100.0f,

                        //    Image = source

                        //}).FirstOrDefault();


                        resultsList.Insert(0, lossStr);
                        PlaySound();
                    });
                
            }

            elaborate = false;
        }

        private async Task LoadModelAsync()
        {
            //Load a machine learning model
            var modelFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///model.onnx"));
            ModelGen = await modelModel.CreateFromStreamAsync(modelFile);
            
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            var fileOpenPicker = new FileOpenPicker();

            fileOpenPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

            fileOpenPicker.FileTypeFilter.Add(".bmp");

            fileOpenPicker.FileTypeFilter.Add(".jpg");

            fileOpenPicker.FileTypeFilter.Add(".png");

            fileOpenPicker.ViewMode = PickerViewMode.Thumbnail;

            var selectedStorageFile = await fileOpenPicker.PickSingleFileAsync();

            SoftwareBitmap softwareBitmap;

            using (var stream = await selectedStorageFile.OpenAsync(FileAccessMode.Read))

            {
                // Create the decoder from the stream 

                var decoder = await BitmapDecoder.CreateAsync(stream);

                // Get the SoftwareBitmap representation of the file in BGRA8 format

                softwareBitmap = await decoder.GetSoftwareBitmapAsync();


                softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied);
            }

            var inputImage = VideoFrame.CreateWithSoftwareBitmap(softwareBitmap);

            EvaluateVideoFrameAsync(inputImage);
        }


        public static IAsyncOperation<VideoFrame> CenterCropImageAsync(VideoFrame inputVideoFrame, uint targetWidth,
            uint targetHeight)
        {
            return AsyncInfo.Run(async token =>
            {
                var useDX = inputVideoFrame.SoftwareBitmap == null;
                VideoFrame result = null;
                // Center crop
                try

                {
                    // Since we will be center-cropping the image, figure which dimension has to be clipped
                    var frameHeight = useDX
                        ? inputVideoFrame.Direct3DSurface.Description.Height
                        : inputVideoFrame.SoftwareBitmap.PixelHeight;
                    var frameWidth = useDX
                        ? inputVideoFrame.Direct3DSurface.Description.Width
                        : inputVideoFrame.SoftwareBitmap.PixelWidth;
                    // Create the VideoFrame to be bound as input for evaluation
                    if (useDX)
                    {
                        if (inputVideoFrame.Direct3DSurface == null)
                            throw new Exception("Invalid VideoFrame without SoftwareBitmap nor D3DSurface");


                        result = new VideoFrame(BitmapPixelFormat.Bgra8,
                            (int) targetWidth,
                            (int) targetHeight,
                            BitmapAlphaMode.Premultiplied);
                    }


                    else


                    {
                        result = new VideoFrame(BitmapPixelFormat.Bgra8,
                            (int) targetWidth,
                            (int) targetHeight,
                            BitmapAlphaMode.Premultiplied);
                    }


                    await inputVideoFrame.CopyToAsync(result);
                }


                catch (Exception ex)


                {
                    Debug.WriteLine(ex.ToString());
                }


                return result;
            });
        }

        public async void PlaySound()
        {
            
            var folder = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFolderAsync("Assets");
            var file = await folder.GetFileAsync("sound.wav");
            var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
            me.SetSource(stream, "");
            me.Play();
        }

        private async void ButtonVideo_OnClick(object sender, RoutedEventArgs e)
        {
            await CameraPreviewControl.StartAsync();
            CameraPreviewControl.CameraHelper.FrameArrived += CameraPreviewControl_FrameArrived;
        }

        private async void ButtonVideoClose_OnClick(object sender, RoutedEventArgs e)
        {
            CameraPreviewControl.CameraHelper.FrameArrived -= CameraPreviewControl_FrameArrived;
            CameraPreviewControl.Stop();
        }
    }
}