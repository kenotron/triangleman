using System;
using System.Drawing;
using System.IO;
using System.Xml;
using SharpDX.Direct2D1;

namespace SvgImport
{
    public enum Command
    {
        MoveTo,
        LineTo,
        CurveTo,
        ClosePath
    }

    public class MyTessellationSink : TessellationSink, IDisposable
    {
        private FileStream fileStream;

        public MyTessellationSink(FileStream fileStream)
        {
            this.fileStream = fileStream;
        }

        public void AddTriangles(Triangle[] triangles)
        {
            foreach (var triangle in triangles)
            {
                string line = string.Format("{0},{1} {2},{3} {4},{5}\n",
                        triangle.Point1.X,
                        triangle.Point1.Y,
                        triangle.Point2.X,
                        triangle.Point2.Y,
                        triangle.Point3.X,
                        triangle.Point3.Y);

                byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(line);
                fileStream.Write(byteArray, 0, byteArray.Length);
            }
        }

        public void Close()
        {
        }

        public IDisposable Shadow
        {
            get;
            set;
        }

        public void Dispose()
        {
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(@"c:\users\ken\desktop\drawing.svg");

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("svg", "http://www.w3.org/2000/svg");

            XmlNodeList selection = doc.SelectNodes("//svg:path", nsmgr);

            Factory factory = new Factory(FactoryType.SingleThreaded);
            PathGeometry pathGeometry = new PathGeometry(factory);

            GeometrySink sink = pathGeometry.Open();

            sink.SetSegmentFlags(PathSegment.ForceRoundLineJoin);
            sink.SetFillMode(FillMode.Winding);

            PointF currentPoint = new PointF(0, 0);

            foreach (XmlNode n in selection)
            {
                XmlAttribute attribute = n.Attributes["d"];
                string d = attribute.Value;

                string[] data = d.Split(' ');

                Command command = Command.MoveTo;
                PointF relativePoint = currentPoint;
                bool isBegun = false;
                FigureEnd figureEndStyle = FigureEnd.Open;

                for (int i = 0; i < data.Length; i++)
                {
                    string arg = data[i];

                    switch (arg.ToUpper())
                    {
                        case "M":
                            command = Command.MoveTo;
                            relativePoint = arg.ToUpper() == arg ? new PointF(0, 0) : currentPoint;
                            break;

                        case "C":
                            command = Command.CurveTo;
                            relativePoint = arg.ToUpper() == arg ? new PointF(0, 0) : currentPoint;
                            break;

                        case "L":
                            command = Command.LineTo;
                            relativePoint = arg.ToUpper() == arg ? new PointF(0, 0) : currentPoint;
                            break;

                        case "V":
                        case "H":
                            break;

                        case "Z":
                            figureEndStyle = FigureEnd.Closed;
                            break;
                        
                        default:
                            currentPoint = CoordinateToPointF(arg, relativePoint);

                            switch (command)
                            {
                                case Command.MoveTo:
                                    if (isBegun)
                                    {
                                        sink.EndFigure(figureEndStyle);
                                    }

                                    sink.BeginFigure(currentPoint, FigureBegin.Filled);
                                    isBegun = true;
                                    
                                    break;

                                case Command.LineTo:
                                    sink.AddLine(currentPoint);
                                    break;

                                case Command.CurveTo:
                                    BezierSegment bezier = new BezierSegment();

                                    bezier.Point1 = currentPoint;
                                    bezier.Point2 = CoordinateToPointF(data[i + 1], relativePoint);
                                    bezier.Point3 = CoordinateToPointF(data[i + 2], relativePoint);

                                    i += 2;

                                    sink.AddBezier(bezier);
                                    break;
                            }

                            break;
                    }                    
                }

                sink.EndFigure(figureEndStyle);
                sink.Close();

                var pathGeometryNew = new PathGeometry(factory);
                var newSink = pathGeometryNew.Open();
                pathGeometry.Widen(4, newSink);
                newSink.Close();
                
                // Now do Tessellation
                using (FileStream fs = File.OpenWrite(@"c:\Users\ken\desktop\my.triangles"))
                {
                    MyTessellationSink tessellationSink = new MyTessellationSink(fs);
                    var result = pathGeometryNew.Tessellate(tessellationSink);
                }
            }

            Console.ReadKey();
        }

        private static PointF CoordinateToPointF(string arg, PointF relativePoint)
        {
            string[] coordinates = arg.Split(',');
            return new PointF(Single.Parse(coordinates[0]) + relativePoint.X, Single.Parse(coordinates[1]) + relativePoint.Y);
        }
    }
}
