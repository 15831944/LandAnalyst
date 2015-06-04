using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using netDxf.Entities;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Loowoo.LandAnalyst.WebInterface
{
    public class CategoryAreaCalculator
    {
        private static readonly string DLBM_FIELD = "DLMC";
        private static readonly string ZLBM_FIELD = "ZLDWMC";
        private static readonly string QSBM_FIELD = "QSDWMC";
        private static readonly string LWMJ_FIELD = "MJ";
        private static readonly string XWKD_FIELD = "KD";

        public IFeatureClass dltbFC { get; set; }
        public IFeatureClass xzdwFC { get; set; }
        public IFeatureClass lxdwFC { get; set; }

        public bool CheckCoordinates(LwPolyline line)
        {
            var lyr = new FeatureLayerClass();
            lyr.FeatureClass = dltbFC;
            var ext = lyr.Extent;
            
            foreach (var vertex in line.Vertexes)
            {
                if (vertex.Location.X < ext.XMin || vertex.Location.X > ext.XMax || vertex.Location.Y < ext.YMin ||
                    vertex.Location.Y > ext.YMax)
                {
                    return false;
                }
            }
            return true;
        }

        public IPolygon GeneratePolygon(LwPolyline line)
        {
            var pg = new PolygonClass();
            var pc = (IPointCollection) pg;
            var o = Type.Missing;
            var vertexes = line.PoligonalVertexes(int.Parse(ConfigurationManager.AppSettings["BulgePrecision"]), double.Parse(ConfigurationManager.AppSettings["WeldThreshold"]), double.Parse(ConfigurationManager.AppSettings["BulgeThreshold"]));
            foreach (var vertex in vertexes)
            {
                var pt = new PointClass();
                pt.PutCoords(vertex.X, vertex.Y);
                pc.AddPoint(pt, ref o, ref o);
            }

            var pt1 = pc.get_Point(0);
            var pt2 = pc.get_Point(pc.PointCount - 1);
            if (Math.Abs(pt1.X - pt2.X) > double.Epsilon || Math.Abs(pt1.Y - pt2.Y) > double.Epsilon)
            {
                var pt = new PointClass();
                pt.PutCoords(pt1.X, pt1.Y);
                pc.AddPoint(pt, ref o, ref o);
            }
            return pg;
        }

        public List<IntersectRecord> Calculate(IPolygon polygon)
        {
            var dict = new Dictionary<string, IntersectRecord>();
            Merge(Calc4DLTB(polygon), dict, 1.0);
            if (lxdwFC != null)
            {
                Merge(Calc4LXDW(polygon), dict, -1.0);
            }
            if (xzdwFC != null)
            {
                Merge(Calc4XZDW(polygon), dict, -1.0);
            }
            Round(dict);
            return dict.Values.ToList();
        }

        public void Round(Dictionary<string, IntersectRecord> dict)
        {
            foreach (var pair in dict)
            {
                pair.Value.Value = Math.Round(pair.Value.Value/10000, 4);
            }
        }

        private void Merge(IList<IntersectRecord> from, IDictionary<string, IntersectRecord> to, double coefficient)
        {
            foreach (var rec in from)
            {
                var key = string.Format("{0}-{1}", rec.ZLBM, rec.DLBM);
                
                if (!to.ContainsKey(key) )
                {
                    if (coefficient > 0)
                    {
                        to.Add(key, rec);
                    }
                }
                else
                {
                    to[key].Value += coefficient*rec.Value;
                }
               
            }
        }

        private static double CalcArea(IGeometry geo)
        {
            if (geo.IsEmpty) return .0;
            return (geo is IArea) ? (geo as IArea).Area : .0;
        }

        private static double CalcLength(IGeometry geo)
        {
            if (geo.IsEmpty) return .0;
            return (geo is IPolyline) ? (geo as IPolyline).Length : .0;
        }

        private IList<IntersectRecord> Calc4XZDW(IPolygon polygon)
        {
            var cursor = xzdwFC.Search(
                new SpatialFilterClass() { Geometry = polygon, SpatialRel = esriSpatialRelEnum.esriSpatialRelEnvelopeIntersects },
                false);

            var to = (ITopologicalOperator)polygon;
            
            var index1 = cursor.FindField(DLBM_FIELD);
            
            var index3 = cursor.FindField(XWKD_FIELD);
            var index2 = cursor.FindField(QSBM_FIELD + "1");
            var index4 = cursor.FindField(QSBM_FIELD + "2");
            var list = new List<IntersectRecord>();

            var exteriorRings = PolygonRingsToPolylines(polygon);
            
            IFeature feature = cursor.NextFeature();
            while (feature != null)
            {
                IGeometry geo = feature.ShapeCopy;
                if (geo.IsEmpty == false)
                {
                    geo.SpatialReference = polygon.SpatialReference;
                    var geo2 = to.Intersect(geo, esriGeometryDimension.esriGeometry1Dimension);
                    var length = CalcLength(geo2);

                    var width = double.Parse(feature.get_Value(index3).ToString());
                    var area = length*width;
                    length = .0;
                    foreach (var ring in exteriorRings)
                    {
                        var geo3 = (ring as ITopologicalOperator).Intersect(geo2,
                                                                           esriGeometryDimension.esriGeometry1Dimension);
                        length += CalcLength(geo3);
                    }

                    area = area - length*width*0.5;
                    if (area > double.Epsilon)
                    {
                        if (feature.get_Value(index4) == null || (feature.get_Value(index4) is DBNull) ||
                            string.IsNullOrEmpty(feature.get_Value(index4).ToString().Trim()))
                        {
                            var rec = new IntersectRecord()
                                {
                                    DLBM = feature.get_Value(index1).ToString(),
                                    ZLBM = feature.get_Value(index2).ToString(),
                                    Value = area
                                };
                            list.Add(rec);
                        }
                        else
                        {
                            var rec = new IntersectRecord()
                                {
                                    DLBM = feature.get_Value(index1).ToString(),
                                    ZLBM = feature.get_Value(index2).ToString(),
                                    Value = area*0.5
                                };
                            list.Add(rec);

                            rec = new IntersectRecord()
                                {
                                    DLBM = feature.get_Value(index1).ToString(),
                                    ZLBM = feature.get_Value(index4).ToString(),
                                    Value = area*0.5
                                };
                            list.Add(rec);
                        }
                    }
                }

                feature = cursor.NextFeature();
            }

            Marshal.ReleaseComObject(cursor);
            return list;
        }

        private IList<IntersectRecord> Calc4LXDW(IPolygon polygon)
        {
            var cursor = lxdwFC.Search(
                new SpatialFilterClass() {Geometry = polygon, SpatialRel = esriSpatialRelEnum.esriSpatialRelContains},
                false);

            var index1 = cursor.FindField(DLBM_FIELD);
            var index2 = cursor.FindField(QSBM_FIELD);
            var index3 = cursor.FindField(LWMJ_FIELD);

            var list = new List<IntersectRecord>();
            IFeature feature = cursor.NextFeature();
            while (feature != null)
            {
                var geo = feature.ShapeCopy;
                if (geo.IsEmpty == false)
                {
                    var rec = new IntersectRecord()
                        {
                            DLBM = feature.get_Value(index1).ToString(),
                            ZLBM = feature.get_Value(index2).ToString(),
                            Value = double.Parse(feature.get_Value(index3).ToString())
                        };
                    list.Add(rec);
                }

                feature = cursor.NextFeature();
            }

            Marshal.ReleaseComObject(cursor);
            return list;
        }

        private static IEnumerable<IPolyline> PolygonRingsToPolylines(IPolygon polygon)
        {
            var polygon4 = (IPolygon4)polygon;
            var exteriorRingGeometryCollection = (IGeometryCollection)polygon4.ExteriorRingBag;
            
            for (int i = 0; i < exteriorRingGeometryCollection.GeometryCount; i++)
            {
                var exteriorRingGeometry = exteriorRingGeometryCollection.get_Geometry(i);
                yield return SegmentCollectionToPolyline((ISegmentCollection) exteriorRingGeometry);
            }
        }

        private static IPolyline SegmentCollectionToPolyline(ISegmentCollection segmentCollection)
        {
            var polyline = (IPolyline)new PolylineClass();
            var geomcoll = (IGeometryCollection)polyline;
            var pathcoll = (ISegmentCollection)new PathClass();
            for (int i = 0; i < segmentCollection.SegmentCount; i++)
            {
                var segment = segmentCollection.get_Segment(i);
                pathcoll.AddSegment(segment);
            }
            geomcoll.AddGeometry((IGeometry)pathcoll);
            geomcoll.GeometriesChanged();
            return polyline;
        }

        private IList<IntersectRecord> Calc4DLTB(IPolygon polygon)
        {
            var cursor = dltbFC.Search(
                new SpatialFilterClass() {Geometry = polygon, SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects},
                false);

            var pg5 = (IPolygon4) polygon;
            pg5.SimplifyEx(true, true, true);

            
            var to = (ITopologicalOperator) polygon;
            //to.IsKnownSimple = false;
            if (to.IsSimple == false)
                to.Simplify();

            var index1 = cursor.FindField(DLBM_FIELD);
            var index2 = cursor.FindField(QSBM_FIELD);
            var list = new List<IntersectRecord>();
            IFeature feature = cursor.NextFeature();
            while (feature != null)
            {
                IGeometry geo = feature.ShapeCopy;
                if (geo.IsEmpty == false)
                {
                    var geo2 = to.Intersect(geo, esriGeometryDimension.esriGeometry2Dimension);
                    var area = 0.0;
                    if (geo2 is IArea)
                    {
                        area = (geo2 as IArea).Area;
                    }

                    if (area > double.Epsilon)
                    {
                        var rec = new IntersectRecord()
                            {
                                DLBM = feature.get_Value(index1).ToString(),
                                ZLBM = feature.get_Value(index2).ToString(),
                                Value = area
                            };
                        list.Add(rec);
                    }
                }

                feature = cursor.NextFeature();
            }

            Marshal.ReleaseComObject(cursor);
            return list;
        }
    }
}
