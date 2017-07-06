﻿using System.Linq;
using VcEngineAutomation;
using VcEngineAutomation.Models;
using VcEngineAutomation.Panels;

namespace VcEngineAutomationTester
{
    /// <summary>
    /// Dummy program to quickly test automation's
    /// </summary>
    static class Program
    {
        static void Main(string[] args)
        {
            if (args.Contains("test-launch"))
            {
                Launch();
            }
            else if (args.Contains("test-camera"))
            {
                TestCamera();
            }
            else if (args.Contains("test-compproperties"))
            {
                TestComponentProperties();
            }
            else if (args.Contains("test-loadcomponent"))
            {
                TestLoadComponent();
            }
            else if (args.Contains("test-output"))
            {
                TestOutput();
            }
            else if (args.Contains("test-misc"))
            {
                TestMisc();
            } else if (args.Contains("test-all"))
            {
                var eng = VcEngine.Attach();
                eng.World.Clear();
                eng.World.LoadComponentByVcid("9d111ec4-5c75-4ff0-96fe-03dc3788a632");
                eng.PropertiesPanel.Position = new Position(100,200,300);
                eng.PropertiesPanel.SetProperty("Name", "new name");
                eng.MoveFocusTo3DViewPort();
                eng.Camera.FillView();
                var output = eng.OutputPanel.Text;
                eng.World.SelectAll();
                eng.World.CopyAndPasteSelectedComponents();
                eng.World.DeleteSelectedComponent();
                eng.World.Clear();
            }
        }

        private static void TestMisc()
        {
            var eng = VcEngine.Attach();
            eng.World.RenameSelectedComponent("RenameSelectedComponent");
        }

        private static void TestOutput()
        {
            var eng = VcEngine.Attach();
            var text = eng.OutputPanel.Text;
        }

        private static void TestLoadComponent()
        {
            var eng = VcEngine.Attach();
            eng.World.LoadComponentByVcid("9d111ec4-5c75-4ff0-96fe-03dc3788a632");
        }

        private static void TestComponentProperties()
        {
            // Select a component in the engine
            var eng = VcEngine.Attach();
            var pos = eng.PropertiesPanel.Position;
            pos.X += 100;
            eng.PropertiesPanel.Position = pos;

            var category = eng.PropertiesPanel.GetProperty("Category");
            eng.PropertiesPanel.SetProperty("Category", $"{category}-New");
        }

        private static void TestCamera()
        {
            var eng = VcEngine.Attach();
            eng.Camera.FillView();
        }

        private static void Launch()
        {
            var vcEngine = VcEngine.AttachOrLaunch(@"C:\Program Files\Visual Components\Visual Components Professional 4.0.4");
            vcEngine.Ribbon.DrawingTab.Select();
            vcEngine.Application.Close();
        }
    }
}