using Elements;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Elements.Serialization.JSON;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace JSONInspector
{
    public static class JSONInspector
    {
        /// <summary>
        /// The JSONInspector function.
        /// </summary>
        /// <param name="model">The input model.</param>
        /// <param name="input">The arguments to the execution.</param>
        /// <returns>A JSONInspectorOutputs instance containing computed results and the model with any new elements.</returns>
        public static JSONInspectorOutputs Execute(Dictionary<string, Model> inputModels, JSONInspectorInputs input)
        {
            var output = new JSONInspectorOutputs();
            if (!string.IsNullOrEmpty(input.JSON))
            {
                object deserializedAnon = null;
                try
                {
                    deserializedAnon = JsonConvert.DeserializeObject(input.JSON);
                }
                catch
                {
                    // assume json was escaped and try to unescape it
                    try
                    {
                        static string Unescape(string str)
                        {
                            return str
                                .Replace("\\n", "\n")
                                .Replace("\\r", "\r")
                                .Replace("\\t", "\t")
                                .Replace("\\\"", "\"")
                                .Replace("\\\\", "\\");
                        }
                        deserializedAnon = JsonConvert.DeserializeObject(Unescape(input.JSON));
                    }
                    catch
                    {
                        output.Warnings.Add($"Could not deserialize {input.JSON}");
                    }
                }
                if (deserializedAnon == null)
                {
                    return output;
                }
                // check if it's an array
                if (deserializedAnon is JArray array)
                {
                    foreach (var item in array)
                    {
                        if (item is JObject jObject)
                        {
                            ProcessObject(jObject, output);
                        }
                    }
                }
                else if (deserializedAnon is JObject jObject)
                {
                    ProcessObject(jObject, output);
                }
            }
            return output;
        }

        private static Random Random = new Random(11);
        private static void ProcessObject(JObject item, JSONInspectorOutputs output)
        {
            try
            {
                // If it has "Transform" and "Elements" properties, it's a model. Use Model.FromJson to deserialize it.
                if (item.ContainsKey("Transform") && item.ContainsKey("Elements"))
                {
                    var model = Model.FromJson(item.ToString());
                    output.Model.AddElements(model.Elements.Values);
                }
                else if (item.ContainsKey("Perimeter")) // profile
                {
                    var profile = JsonConvert.DeserializeObject<Profile>(item.ToString());
                    var geoElem = new GeometricElement
                    {
                        Representation = new Lamina(profile),
                        Material = Random.NextMaterial()
                    };
                    output.Model.AddElement(geoElem);
                    output.Model.AddElements(profile.ToModelCurves());
                    profile.Perimeter.Vertices.ToList().ForEach(v => output.Model.AddElement(RenderPoint(v)));
                    profile.Voids?.ToList()?.ForEach(v => v.Vertices.ToList().ForEach(vv => output.Model.AddElement(RenderPoint(vv))));
                }
                else if (item.ContainsKey("Id") && item.ContainsKey("discriminator") && item.ContainsKey("Representation"))
                {
                    try
                    {
                        var element = JsonConvert.DeserializeObject<GeometricElement>(item.ToString());
                        output.Model.AddElement(element);
                    }
                    catch
                    {
                        output.Warnings.Add($"Could not deserialize {item["discriminator"]} with id {item["Id"]}");
                    }
                }
                else if (item.ContainsKey("Id") && item.ContainsKey("discriminator"))
                {
                    try
                    {
                        var element = JsonConvert.DeserializeObject<GenericElement>(item.ToString());
                        element.discriminator = item["discriminator"].ToString();
                        element.AdditionalProperties["discriminator"] = item["discriminator"].ToString();
                        output.Model.AddElement(element);
                    }
                    catch
                    {
                        output.Warnings.Add($"Could not deserialize {item["discriminator"]} with id {item["Id"]}");
                    }
                }
                else if (item.ContainsKey("Vertices"))
                {
                    var poly = JsonConvert.DeserializeObject<Polyline>(item.ToString());
                    output.Model.AddElement(new ModelCurve(poly));
                }
                else if (item.ContainsKey("X") && item.ContainsKey("Y") && item.ContainsKey("Z"))
                {
                    var vec = JsonConvert.DeserializeObject<Vector3>(item.ToString());
                    output.Model.AddElement(RenderPoint(vec));
                }
                else if (item.ContainsKey("Matrix"))
                {
                    var transform = JsonConvert.DeserializeObject<Transform>(item.ToString());
                    output.Model.AddElements(transform.ToModelCurves());
                }
                else if (item.ContainsKey("Min") && item.ContainsKey("Max"))
                {
                    var bbox = JsonConvert.DeserializeObject<BBox3>(item.ToString());
                    output.Model.AddElements(bbox.ToModelCurves());
                }
                else
                {
                    output.Warnings.Add($"Could not deserialize {item}");
                }
            }
            catch
            {
                output.Warnings.Add($"Could not deserialize {item}");
            }
        }

        private static MeshElement PointMesh = null;
        private static Element RenderPoint(Vector3 vec)
        {
            if (PointMesh == null)
            {
                var m = Mesh.Sphere(0.1, 10);
                PointMesh = new MeshElement(m, new Transform())
                {
                    IsElementDefinition = true,
                    Material = BuiltInMaterials.XAxis
                };
            }
            return PointMesh.CreateInstance(new Transform(vec), $"Point {vec.X:0.0}, {vec.Y:0.0}, {vec.Z:0.0}");
        }

    }
}