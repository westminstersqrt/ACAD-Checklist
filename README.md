# AutoCAD 2026 — Interactive Checklist Prototype

## What this prototype does
- `CLNEW`: creates a circle + text label
- `CLTOGGLE`: clickability of checklist circles and toggles their state
- Task state is stored as XData on the circle, so it is saved in the DWG
- Checklist geometry is placed on the `CHECKLIST` layer (layer is creating automatically)

## Description
In this prototype, start `CLTOGGLE`, then click the circles. A production-grade always-on interaction can be implemented with a custom command/reactor workflow, with careful handling of selection, grips, command cancellation, and document switching
The current checked state is indicated by changing the circle color

## Build
1. Visual Studio 2022 with the .NET desktop development workload
2. .NET 8 SDK for AutoCAD 2026, Update 1.1 or earlier 
   .NET 10 SDK for AutoCAD 2026 Update 1.2+ 
   !!!use the matching target and SDK if building specifically for that host!!!
3. Open `AcadChecklistPrototype.csproj`
4. Set the `ACAD_DIR` environment variable to the folder containing `acad.exe`, `AcMgd.dll`, `AcDbMgd.dll`, and `AcCoreMgd.dll`
5. Build x64

## Load in AutoCAD
1. Run `NETLOAD`
2. Select `AcadChecklistPrototype.dll` from `bin\\Debug\\net8.0-windows` or `bin\\Release\\net8.0-windows`
3. Run `CLHELP` to see the commands
4. Run `CLNEW` to create a task
5. Run `CLTOGGLE` and click a circle to toggle its state
6. Save the drawing as DWG, close/reopen it, reload the plugin, and test the state

## TODO
- Draw a real checkmark using two `Line` entities, tied to the task by handles
- Store list ID, order, text entity handle, circle handle, and status in XData/Xrecord
- Add `CLADD`, `CLDELETE`, `CLMOVE`, and `CLRESET`
- Add a palette/toolbar button and evaluate an always-on click workflow
- Support rotated UCS, annotative scale, multiline wrapping, and copying/moving the grouped list