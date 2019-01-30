using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ImageWrite.Helpers;
using ImageWrite.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using TensorFlow;

namespace ImageWrite.Classes
{
    public class ImageWriter : IImageWriter
    {
        private static IEnumerable<CatalogItem> _catalog;

        private static double MIN_SCORE_FOR_OBJECT_HIGHLIGHTING = 0.6;
        public async Task<string> UploadImage(IFormFile file)
        {
            if (CheckIfImageFile(file))
            {
                return await WriteFile(file);
            }

            return "Invalid image file";
        }
        
        private bool CheckIfImageFile(IFormFile file)
        {
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                file.CopyTo(ms);
                fileBytes = ms.ToArray();
            }

            return WriterHelper.GetImageFormat(fileBytes) != WriterHelper.ImageFormat.unknown;
        }
        
        public async Task<string> WriteFile(IFormFile file)
        {
            string fileName;
            try
            {
                var extension = "." + file.FileName.Split('.')[file.FileName.Split('.').Length - 1];
                fileName = Guid.NewGuid().ToString() + extension; //Create a new Name for the file due to security reasons.
                var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\images", fileName);

                using (var bits = new FileStream(path, FileMode.Create))
                {
                    file.CopyTo(bits);
                }
            }
            catch (Exception e)
            {
                return e.Message;
            }

            var newFileName = ProcessImage(fileName);

            return await Task.Run(() => newFileName);
        }

        private static string ProcessImage(string fileName)
        {

            var catalogPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "mscoco_label_map.pbtxt");

            var modelPath = DownloadDefaultModel(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));

            _catalog = CatalogUtil.ReadCatalogItems(catalogPath);
            var _input = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\images", fileName);
            var newFileName = fileName.Split('.')[0] + "CONVERTED." + fileName.Split('.')[1];
            var _output = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot\\images", newFileName);
            var fileTuples = new List<(string input, string output)>() { (_input, _output) };
            string modelFile = modelPath;

            try
            {
                using (var graph = new TFGraph())
                {
                    var model = File.ReadAllBytes(modelFile);
                    graph.Import(new TFBuffer(model));

                    using (var session = new TFSession(graph))
                    {
                        Console.WriteLine("Detecting objects");

                        foreach (var tuple in fileTuples)
                        {
                            var tensor = ImageUtil.CreateTensorFromImageFile(tuple.input, TFDataType.UInt8);
                            var runner = session.GetRunner();


                            runner
                                .AddInput(graph["image_tensor"][0], tensor)
                                .Fetch(
                                graph["detection_boxes"][0],
                                graph["detection_scores"][0],
                                graph["detection_classes"][0],
                                graph["num_detections"][0]);
                            var output = runner.Run();

                            var boxes = (float[,,])output[0].GetValue(jagged: false);
                            var scores = (float[,])output[1].GetValue(jagged: false);
                            var classes = (float[,])output[2].GetValue(jagged: false);
                            var num = (float[])output[3].GetValue(jagged: false);

                            DrawBoxes(boxes, scores, classes, tuple.input, tuple.output, MIN_SCORE_FOR_OBJECT_HIGHLIGHTING);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.Write(e.Message);
            }

            return newFileName;
        }

        private static string DownloadDefaultModel(string dir)
        {
            string defaultModelUrl = "http://download.tensorflow.org/models/object_detection/faster_rcnn_inception_resnet_v2_atrous_coco_11_06_2017.tar.gz";

            var modelFile = Path.Combine(dir, "faster_rcnn_inception_resnet_v2_atrous_coco_11_06_2017/frozen_inference_graph.pb");
            var zipfile = Path.Combine(dir, "faster_rcnn_inception_resnet_v2_atrous_coco_11_06_2017.tar.gz");

            if (File.Exists(modelFile))
                return modelFile;

            if (!File.Exists(zipfile))
            {
                Console.WriteLine("Downloading default model");
                var wc = new WebClient();
                wc.DownloadFile(defaultModelUrl, zipfile);
            }

            ExtractToDirectory(zipfile, dir);
            File.Delete(zipfile);

            return modelFile;
        }

        private static void ExtractToDirectory(string file, string targetDir)
        {
            Console.WriteLine("Extracting");

            using (Stream inStream = File.OpenRead(file))
            using (Stream gzipStream = new GZipInputStream(inStream))
            {
                TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream);
                tarArchive.ExtractContents(targetDir);
            }
        }

        private static void DrawBoxes(float[,,] boxes, float[,] scores, float[,] classes, string inputFile, string outputFile, double minScore)
        {
            var x = boxes.GetLength(0);
            var y = boxes.GetLength(1);
            var z = boxes.GetLength(2);

            float ymin = 0, xmin = 0, ymax = 0, xmax = 0;

            using (var editor = new ImageEditor(inputFile, outputFile))
            {
                for (int i = 0; i < x; i++)
                {
                    for (int j = 0; j < y; j++)
                    {
                        if (scores[i, j] < minScore) continue;

                        for (int k = 0; k < z; k++)
                        {
                            var box = boxes[i, j, k];
                            switch (k)
                            {
                                case 0:
                                    ymin = box;
                                    break;
                                case 1:
                                    xmin = box;
                                    break;
                                case 2:
                                    ymax = box;
                                    break;
                                case 3:
                                    xmax = box;
                                    break;
                            }

                        }

                        int value = Convert.ToInt32(classes[i, j]);
                        CatalogItem catalogItem = _catalog.FirstOrDefault(item => item.Id == value);
                        if (catalogItem != null)
                            editor.AddBox(xmin, xmax, ymin, ymax, $"{catalogItem.DisplayName} : {(scores[i, j] * 100).ToString("0")}%");
                    }
                }
            }
        }
    }
}
