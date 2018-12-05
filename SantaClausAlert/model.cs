using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.AI.MachineLearning;
namespace SantaClausAlert
{
    
    public sealed class modelInput
    {
        public ImageFeatureValue data; // BitmapPixelFormat: Bgra8, BitmapAlphaMode: Premultiplied, width: 227, height: 227
    }
    
    public sealed class modelOutput
    {
        public TensorString ClassLabel = TensorString.Create(new long[] { 1, 1 });
        public IList<IDictionary<string, float>> Loss = new List<IDictionary<string, float>>();
    }
    
    public sealed class modelModel
    {
        private LearningModel model;
        private LearningModelSession session;
        private LearningModelBinding binding;
        public static async Task<modelModel> CreateFromStreamAsync(IRandomAccessStreamReference stream)
        {
            modelModel learningModel = new modelModel();
            learningModel.model = await LearningModel.LoadFromStreamAsync(stream);
            learningModel.session = new LearningModelSession(learningModel.model);
            learningModel.binding = new LearningModelBinding(learningModel.session);
            return learningModel;
        }
        public async Task<modelOutput> EvaluateAsync(modelInput input)
        {
            var output = new modelOutput();
            binding.Bind("data", input.data);
            binding.Bind("classLabel", output.ClassLabel);
            binding.Bind("loss", output.Loss);
            var result = await session.EvaluateAsync(binding, "0");
            
            output.ClassLabel = result.Outputs["classLabel"] as TensorString;
            output.Loss = result.Outputs["loss"] as List<IDictionary<string,float>>;
            return output;
        }
    }
}
