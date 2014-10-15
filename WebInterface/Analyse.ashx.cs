using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using netDxf;
using netDxf.Header;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace Loowoo.LandAnalyst.WebInterface
{
    /// <summary>
    /// Summary description for Analyse
    /// </summary>
    public class Analyse : IHttpHandler
    {
        private static readonly object syncRoot = new object();

        public void ProcessRequest(HttpContext context)
        {
            /*var encoding = Encoding.GetEncoding("GB2312");
            using (var reader = new StreamReader(@"C:\Temp\yg1407.dxf", encoding))
            {
                var content = reader.ReadToEnd();
                var result = Process(content, context.Server.MapPath("~/App_Data/haining.mdb"));
                context.Response.ContentType = "text/plain";
                var serializer = new JavaScriptSerializer();
                context.Response.Write(serializer.Serialize(result));
            }*/

            
            var content = context.Request["file"];
            var result = Process(content, context.Server.MapPath("~/App_Data/haining.mdb"));
            context.Response.ContentType = "text/plain";
            var serializer = new JavaScriptSerializer();
            context.Response.Write(serializer.Serialize(result));
        }

        private ResultPack Process(string content, string mdbFile)
        {
            var pack = new ResultPack();
            if (string.IsNullOrEmpty(content))
            {
                pack.ErrorMessage = "上传的文件内容为空，请提交正确的内容。";
                return pack;
            }
            try
            {
                var encoding = Encoding.GetEncoding("GB2312");
                var bytes = encoding.GetBytes(content);
                using (var stream = new MemoryStream(bytes))
                {
                    bool isBinary;
                    var dxfVersion = DxfDocument.CheckDxfFileVersion(stream, out isBinary);
                    if (dxfVersion < DxfVersion.AutoCad12 && dxfVersion > DxfVersion.AutoCad2010)
                    {
                        pack.ErrorMessage = "系统无法读取当前版本（{0}）的CAD文件，请提交AutoCAD R12至AutoCAD 2010版本生成的DXF文件。";
                        return pack;
                    }

                    var dxf = DxfDocument.Load(stream, dxfVersion < DxfVersion.AutoCad2000);
                    if (dxf == null)
                    {
                        pack.ErrorMessage = "无法识别的dxf文件，上传的dxf文件可能已经损坏。";
                        return pack;
                    }

                    if (dxf.LwPolylines.Count == 0)
                    {
                        pack.ErrorMessage = "CAD文件中无法找到红线。";
                        return pack;
                    }

                    /*if (dxf.LwPolylines.Count > 1)
                {
                    pack.ErrorMessage = "CAD文件中红线数量大于一个，请删除不必要的图形。";
                    return pack;
                }*/

                    var bmp = ImageGenerator.Generate(dxf.LwPolylines[0], new Size(640, 480));

                    using (var bmpStream = new MemoryStream())
                    {
                        bmp.Save(bmpStream, ImageFormat.Jpeg);
                        pack.ImageContent = Convert.ToBase64String(bmpStream.ToArray());
                    }


                    lock (syncRoot)
                    {
                        var aoInit = new ESRI.ArcGIS.esriSystem.AoInitializeClass();
                        if (aoInit.IsProductCodeAvailable(esriLicenseProductCode.esriLicenseProductCodeArcEditor) ==
                            esriLicenseStatus.esriLicenseAvailable)
                        {
                            aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeArcEditor);
                        }
                        else
                        {
                            aoInit.Initialize(esriLicenseProductCode.esriLicenseProductCodeEngine);
                        }
                        var factory = new AccessWorkspaceFactoryClass();
                        var ws = (IFeatureWorkspace) factory.OpenFromFile(mdbFile, 0);

                        var calculator = new CategoryAreaCalculator();

                        calculator.dltbFC = ws.OpenFeatureClass(ConfigurationManager.AppSettings["DLTBLayerName"]);
                        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["XZDWLayerName"]))
                        {
                            calculator.xzdwFC = ws.OpenFeatureClass(ConfigurationManager.AppSettings["XZDWLayerName"]);
                        }

                        if (!string.IsNullOrEmpty(ConfigurationManager.AppSettings["LXDWLayerName"]))
                        {
                            calculator.xzdwFC = ws.OpenFeatureClass(ConfigurationManager.AppSettings["LXDWLayerName"]);
                        }


                        var pg = calculator.GeneratePolygon(dxf.LwPolylines[0]);
                        try
                        {
                            var result = calculator.Calculate(pg);
                            pack.Details = result;
                        }
                        catch (Exception ex)
                        {
                            pack.ErrorMessage = "空间分析时发生错误：" + ex.ToString();
                            return pack;
                        }
                        finally
                        {
                            aoInit.Shutdown();
                        }
                    }
                    return pack;
                }
            }
            catch (Exception ex2)
            {
                pack.ErrorMessage = "进行数据处理时发生错误：" + ex2.ToString();
                return pack;
            }
        
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}