using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitAPI_HW_6
{

    [TransactionAttribute(TransactionMode.Manual)]
    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            List<Level> listLevel = GetLevelList(doc);

            Level level1 = GetLevel(listLevel, 1);

            Level level2 = GetLevel(listLevel, 2);

            double width = 10000;
            double depth = 5000;

            List<Wall> walls = WallsCreation(doc, level1, level2, width, depth);

            AddDoor(doc, level1, walls[0]);

            AddWindow(doc, level1, walls[1]);
            AddWindow(doc, level1, walls[2]);
            AddWindow(doc, level1, walls[3]);

            AddRoof(doc, level2, walls, width, depth, 10);

            return Result.Succeeded;
        }

        private void AddRoof(Document doc, Level level, List<Wall> walls, double width, double depth, double height)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            View view = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .OfType<View>()
                .Where(x => x.Name.Equals("Уровень 1"))
                .FirstOrDefault();

            double wallWidth = walls[0].Width;
            double dt = wallWidth / 2;

            double Width = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double Depth = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);

            double extrusionStart = -Width / 2 - dt;
            double extrusionEnd = Width / 2 + dt;

            double curveStart = -Depth / 2 - dt;
            double curveEnd = Depth / 2 + dt;

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(new XYZ(0, curveStart, level.Elevation), new XYZ(0, 0, level.Elevation + height)));
            curveArray.Append(Line.CreateBound(new XYZ(0, 0, level.Elevation + height), new XYZ(0, curveEnd, level.Elevation)));

            Transaction transaction = new Transaction(doc, "Построение крыши");
            transaction.Start();

            ReferencePlane plane = doc.Create.NewReferencePlane(new XYZ(0, 0, 0), new XYZ(0, 0, 20), new XYZ(0, 20, 0), view);
            ExtrusionRoof extrusionRoof = doc.Create.NewExtrusionRoof(curveArray, plane, level, roofType, extrusionStart, extrusionEnd);
            extrusionRoof.EaveCuts = EaveCutterType.TwoCutSquare;

            transaction.Commit();
        }

        public List<Level> GetLevelList(Document doc)
        {
            List<Level> listLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            return listLevel;
        }

        public Level GetLevel(List<Level> levelList, int levelNum)
        {
            Level level = levelList
                .Where(x => x.Name.Equals($"Уровень {levelNum}"))
                .FirstOrDefault();

            return level;
        }

        public List<Wall> WallsCreation(Document doc, Level level1, Level level2, double width, double depth)
        {
            double Width = UnitUtils.ConvertToInternalUnits(width, UnitTypeId.Millimeters);
            double Depth = UnitUtils.ConvertToInternalUnits(depth, UnitTypeId.Millimeters);

            double dx = Width / 2;
            double dy = Depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            List<Wall> walls = new List<Wall>();

            Transaction transaction = new Transaction(doc, "Построение стен");
            transaction.Start();

            for (int i = 0; i < 4; i++)
            {
                Line line = Line.CreateBound(points[i], points[i + 1]);
                Wall wall = Wall.Create(doc, line, level1.Id, false);

                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(level2.Id);

                walls.Add(wall);
            }

            transaction.Commit();

            return walls;
        }

        private void AddDoor(Document doc, Level level1, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 2134 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point = (point1 + point2) / 2;

            Transaction transaction = new Transaction(doc, "Построение двери");
            transaction.Start();

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(point, doorType, wall, level1, StructuralType.NonStructural);

            transaction.Commit();
        }

        private void AddWindow(Document doc, Level level, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0915 x 1830 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            LocationCurve hostCurve = wall.Location as LocationCurve;
            XYZ point1 = hostCurve.Curve.GetEndPoint(0);
            XYZ point2 = hostCurve.Curve.GetEndPoint(1);
            XYZ point3 = (point1 + point2) / 2;

            BoundingBoxXYZ wallBounding = wall.get_BoundingBox(null);
            XYZ wallCenter = (wallBounding.Max + wallBounding.Min) / 2;

            XYZ point = (point3 + wallCenter) / 2;

            Transaction transaction = new Transaction(doc, "Построение окна");
            transaction.Start();

            if (!windowType.IsActive)
                windowType.Activate();

            doc.Create.NewFamilyInstance(point, windowType, wall, level, StructuralType.NonStructural);

            transaction.Commit();
        }
    }
}
