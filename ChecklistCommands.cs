using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

namespace AcadChecklistPrototype;

public class ChecklistCommands
{
    private const string AppName = "ACAD_CHECKLIST_V1";
    private const string LayerName = "CHECKLIST";
    private const string Marker = "TASK";

    [CommandMethod("CLNEW")]
    public void CreateChecklistTask()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null) return;

        Editor ed = doc.Editor;
        var textOpts = new PromptStringOptions("\nВведите текст задачи: ")
        {
            AllowSpaces = true
        };
        PromptResult textRes = ed.GetString(textOpts);
        if (textRes.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(textRes.StringResult))
            return;

        PromptPointResult pointRes = ed.GetPoint("\nУкажите положение кружка: ");
        if (pointRes.Status != PromptStatus.OK) return;

        Database db = doc.Database;
        using Transaction tr = db.TransactionManager.StartTransaction();

        EnsureRegApp(db, tr);
        EnsureLayer(db, tr);

        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        BlockTableRecord space = (BlockTableRecord)tr.GetObject(
            db.CurrentSpaceId, OpenMode.ForWrite);

        double textHeight = Math.Max(ed.GetCurrentView().Height / 45.0, 2.5);
        double radius = textHeight * 0.32;
        Point3d center = pointRes.Value;
        Vector3d right = ed.GetCurrentView().ViewDirection.CrossProduct(
            ed.GetCurrentView().ViewTwist == 0 ? Vector3d.YAxis : Vector3d.XAxis);
        if (right.Length < 1e-6) right = Vector3d.XAxis;
        right = right.GetNormal();

        // Uses a simple XY-oriented layout
        // Here you can change the circle drawing parameters
        Point3d textPoint = new(center.X + radius * 2.8, center.Y, center.Z);

        var circle = new Circle(center, Vector3d.ZAxis, radius)
        {
            Layer = LayerName,
            Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
        };
        ObjectId circleId = space.AppendEntity(circle);
        tr.AddNewlyCreatedDBObject(circle, true);
        SetTaskData(circle, tr, false);

        var text = new DBText
        {
            Position = textPoint,
            Height = textHeight,
            TextString = textRes.StringResult.Trim(),
            Layer = LayerName,
            Color = Color.FromColorIndex(ColorMethod.ByAci, 7),
            HorizontalMode = TextHorizontalMode.TextLeft,
            VerticalMode = TextVerticalMode.TextVerticalMid,
            AlignmentPoint = textPoint
        };
        ObjectId textId = space.AppendEntity(text);
        tr.AddNewlyCreatedDBObject(text, true);
        SetLinkData(text, circleId);

        tr.Commit();
        ed.WriteMessage("\nЗадача создана. Для переключения запустите CLTOGGLE и щёлкните по кружку.");
    }

    // interaction model for prototype: while CLTOGGLE is active, click a checklist
    // circle to switch it. Press Enter/Esc to finish
    [CommandMethod("CLTOGGLE")]
    public void ToggleChecklistTask()
    {
        Document doc = Application.DocumentManager.MdiActiveDocument;
        if (doc is null) return;

        Editor ed = doc.Editor;
        Database db = doc.Database;
        ed.WriteMessage("\nЩёлкайте по кружкам чек-листа. Enter/Esc — завершить.");

        while (true)
        {
            var opts = new PromptEntityOptions("\nВыберите кружок задачи: ");
            opts.SetRejectMessage("\nНужно выбрать кружок чек-листа.");
            opts.AddAllowedClass(typeof(Circle), exactMatch: true);
            PromptEntityResult res = ed.GetEntity(opts);
            if (res.Status != PromptStatus.OK) break;

            using Transaction tr = db.TransactionManager.StartTransaction();
            Entity? ent = tr.GetObject(res.ObjectId, OpenMode.ForWrite) as Entity;
            if (ent is not Circle circle || !IsChecklistTask(circle))
            {
                ed.WriteMessage("\nЭтот кружок не принадлежит чек-листу.");
                tr.Abort();
                continue;
            }

            bool isChecked = ReadChecked(circle);
            WriteChecked(circle, !isChecked);
            circle.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)(!isChecked ? 3 : 8));
            //TODO: make the circle full

            // checkmark strokes are represented by two linked LINE entities;
            // the first prototype changes the circle color as a robust visible state cue;
            // the next iteration can add persistent checkmark geometry feedback;
            tr.Commit();
            ed.WriteMessage(!isChecked ? "\nЗадача отмечена выполненной." : "\nОтметка снята.");
        }
    }

    [CommandMethod("CLHELP")]
    public void ChecklistHelp()
    {
        Editor? ed = Application.DocumentManager.MdiActiveDocument?.Editor;
        ed?.WriteMessage(
            "\nЧек-лист — прототип команд:\n" +
            "CLNEW — создать пункт (текст + кружок)\n" +
            "CLTOGGLE — щёлкать по кружкам, чтобы менять состояние\n" +
            "Состояние хранится в XData и сохраняется в DWG.\n" +
            "Ограничение: пока это не свободный глобальный клик — CLTOGGLE нужно держать активной.");
    }

    private static void EnsureRegApp(Database db, Transaction tr)
    {
        RegAppTable table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
        if (table.Has(AppName)) return;
        table.UpgradeOpen();
        var record = new RegAppTableRecord { Name = AppName };
        table.Add(record);
        tr.AddNewlyCreatedDBObject(record, true);
    }

    private static void EnsureLayer(Database db, Transaction tr)
    {
        LayerTable table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (table.Has(LayerName)) return;
        table.UpgradeOpen();
        var record = new LayerTableRecord
        {
            Name = LayerName,
            Color = Color.FromColorIndex(ColorMethod.ByAci, 7)
        };
        table.Add(record);
        tr.AddNewlyCreatedDBObject(record, true);
    }

    private static void SetTaskData(Entity entity, Transaction tr, bool checkedState)
    {
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, Marker),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, checkedState ? "1" : "0"));
        entity.XData = data;
    }

    private static void SetLinkData(Entity entity, ObjectId circleId)
    {
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, "LABEL"),
            new TypedValue((int)DxfCode.ExtendedDataHandle, circleId.Handle.ToString()));
        entity.XData = data;
    }

    private static bool IsChecklistTask(Entity entity)
    {
        using ResultBuffer? data = entity.GetXDataForApplication(AppName);
        if (data is null) return false;
        TypedValue[] values = data.AsArray();
        return values.Length >= 3 &&
               values[1].TypeCode == (int)DxfCode.ExtendedDataAsciiString &&
               string.Equals(values[1].Value?.ToString(), Marker, StringComparison.Ordinal);
    }

    private static bool ReadChecked(Entity entity)
    {
        using ResultBuffer? data = entity.GetXDataForApplication(AppName);
        if (data is null) return false;
        TypedValue[] values = data.AsArray();
        return values.Length >= 3 && values[2].Value?.ToString() == "1";
    }

    private static void WriteChecked(Entity entity, bool value)
    {
        using var data = new ResultBuffer(
            new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, Marker),
            new TypedValue((int)DxfCode.ExtendedDataAsciiString, value ? "1" : "0"));
        entity.XData = data;
    }
}
