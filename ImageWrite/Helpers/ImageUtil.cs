using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TensorFlow;

namespace ImageWrite.Helpers
{
    class ImageUtil
    {
        public static TFTensor CreateTensorFromImageFile(string file, TFDataType destinationDataType = TFDataType.Float)
        {
            var contents = File.ReadAllBytes(file);

            var tensor = TFTensor.CreateString(contents);

            TFOutput input, output;

            using (var graph = ConstructGraphToNormalizeImage(out input, out output, destinationDataType))
            {
                using (var session = new TFSession(graph))
                {
                    var normalized = session.Run(
                        inputs: new[] { input },
                        inputValues: new[] { tensor },
                        outputs: new[] { output });

                    return normalized[0];
                }
            }
        }
        
        private static TFGraph ConstructGraphToNormalizeImage(out TFOutput input, out TFOutput output, TFDataType destinationDataType = TFDataType.Float)
        {
            var graph = new TFGraph();
            input = graph.Placeholder(TFDataType.String);

            output = graph.Cast(
                graph.ExpandDims(
                    input: graph.Cast(graph.DecodeJpeg(contents: input, channels: 3), DstT: TFDataType.Float),
                    dim: graph.Const(0, "make_batch")
                    ), destinationDataType);
            return graph;
        }
    }
}
