﻿using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.AutomationElements.Infrastructure;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.UIA3;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FlaUI.Core.Shapes;
using VcEngineAutomation.Extensions;
using VcEngineAutomation.Models;
using VcEngineAutomation.Panels;
using VcEngineAutomation.Ribbons;
using VcEngineAutomation.Utils;
using VcEngineAutomation.Windows;

namespace VcEngineAutomation
{
    public class VcEngine
    {
        private readonly Lazy<AutomationElement> viewPort;

        private readonly DockedTabRetriever dockedTabRetriever;
        public Application Application { get; }
        public AutomationBase Automation { get; }

        public bool IsR7 { get; }
        public bool IsR6 { get; }
        public bool IsR5 { get;  }
        public Window MainWindow { get; }
        public Ribbon Ribbon { get; }
        public string MainWindowName { get; }
        public ApplicationMenu ApplicationMenu { get; }
        public Options Options { get; }
        public Camera Camera { get; }
        public Visual3DToolbar Visual3DToolbar { get; }
        public PropertiesPanel PropertiesPanel => new PropertiesPanel(this, false);
        public PropertiesPanel DrawingPropertiesPanel => new PropertiesPanel(this, true);
        public ECataloguePanel ECataloguePanel { get; }
        public OutputPanel OutputPanel { get; }
        public AutomationElement ViewPort => viewPort.Value;
        public World World { get; }

        public VcEngine(Application application, AutomationBase automation)
        {
            var fileVersionInfo = FileVersionInfo.GetVersionInfo(Process.GetProcessById(application.ProcessId).MainModule.FileName);
            IsR5 = fileVersionInfo.ProductVersion.StartsWith("4.0.2");
            IsR6 = fileVersionInfo.FileVersion.StartsWith("4.0.3");
            IsR7 = fileVersionInfo.FileVersion.StartsWith("4.0.4");

            MainWindowName = fileVersionInfo.FileDescription;
            Application = application;
            Automation = automation;
            Console.WriteLine("Waiting for application main window");

            MainWindow = Retry.WhileException(() => Application.GetMainWindow(automation), TimeSpan.FromMinutes(2), TimeSpan.FromMilliseconds(200));
            dockedTabRetriever = new DockedTabRetriever(MainWindow);

            Console.WriteLine("Waiting for main ribbon");
            Tab mainTab = Retry.WhileException(() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("XamRibbonTabs")).AsTab(), TimeSpan.FromMinutes(2));

            viewPort = new Lazy<AutomationElement>(() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("viewportContentPane")));
            World = new World(this);
            Ribbon = new Ribbon(this, MainWindow, mainTab);
            ApplicationMenu = new ApplicationMenu(this);
            Visual3DToolbar = new Visual3DToolbar(this);
            Options = new Options(ApplicationMenu);
            Camera = new Camera(this);
            OutputPanel = new OutputPanel(this, () => IsR7 ? dockedTabRetriever.GetPane("VcOutput") : dockedTabRetriever.GetPane("Output", "VcOutputContentPane"));
            ECataloguePanel = new ECataloguePanel(this, () => IsR7 ? dockedTabRetriever.GetPane("VcECatalogue") : dockedTabRetriever.GetPane("eCatalog", "VcECatalogueContentPane"));
            
            Console.WriteLine("Waiting for ribbon to become enabled");
            Retry.While(() => Retry.WhileException(() => !Ribbon.HomeTab.TabPage.Properties.IsEnabled.Value, TimeSpan.FromMinutes(2)), TimeSpan.FromMinutes(2));

            Console.WriteLine("Setting main window as foreground");
            MainWindow.SetForeground();
        }

        public CommandPanel GetCommandPanel()
        {
            return new CommandPanel(this, () => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(IsR7 ? "CommandPanelViewModelTabItem" : "CommandPanelViewModelContentPane")));
        }
        public CommandPanel GetCommandPanel(string startOfTitle)
        {
            return new CommandPanel(this, () => IsR7 ? dockedTabRetriever.GetPane("CommandPanelViewModel") : dockedTabRetriever.GetPane(startOfTitle, "CommandPanelViewModelContentPane"));
        }

        public void WaitWhileBusy(TimeSpan? waitTimeSpan = null)
        {
            var aMessageBoxWindow = Retry.WhileException(() =>
                    MainWindow.FindFirstChild(cf => cf.ByControlType(ControlType.Window).And(cf.ByClassName("#32770")).Or(cf.ByAutomationId("TextboxDialog"))),
                waitTimeSpan ?? TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(50));
            if (aMessageBoxWindow != null) return;

            if (ShellIsBusy())
            {
                bool shellIsStillBusy = Retry.While(ShellIsBusy, isBusy => isBusy, waitTimeSpan ?? TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(0.5));
                if (shellIsStillBusy) throw new TimeoutException("Timeout while waiting for progress bar to disappear");
            }
        }

        private bool ShellIsBusy()
        {
            Helpers.WaitUntilResponsive(MainWindow);
            if (MainWindow.Patterns.Window.Pattern.WindowInteractionState.Value == WindowInteractionState.ReadyForUserInteraction) return false;
            CheckForCrash();
            if (MainWindow.FindFirstChild(cf => cf.ByAutomationId("ProgressBarDialog")) != null)
            {
                return true;
            }
            var currentPropertyValue = MainWindow.Properties.HelpText;
            return currentPropertyValue != null && ((string)currentPropertyValue).Contains("Busy");
        }
        public virtual void CheckForCrash()
        {
            MainWindow.WaitWhileBusy();
            if (MainWindow.Patterns.Window.Pattern.WindowInteractionState.Value == WindowInteractionState.ReadyForUserInteraction) return;

            Window[] windows = MainWindow.FindModalWindowsProtected();
            // Catch normal VC exception stack trace
            Window window = windows.FirstOrDefault(w => w.Properties.Name.ValueOrDefault == MainWindowName && w.Properties.AutomationId.ValueOrDefault == "_this");
            if (window != null)
            {
                var text = VcMessageBox.GetTextAndClose(this);
                if (text.ToLower().Contains("unhandled exception")) throw new InvalidOperationException(text);
            }
        }

        public void MoveFocusTo3DViewPort()
        {
            MoveMouseTo3DViewPort();
            Mouse.Click(MouseButton.Middle);
            WaitWhileBusy();
        }
        public void MoveMouseTo3DViewPort(Point moveOffset = null)
        {
            Mouse.MoveTo(viewPort.Value.GetCenter());
            if (moveOffset != null)
            {
                Mouse.MoveBy((int)moveOffset.X, (int)moveOffset.Y);
            }
        }
        public void LoadLayout(string layoutFile, bool closeMandatoryUpdateWindow = true)
        {
            string fileToLoad = GetFileToLoad(layoutFile);
            AutomationElement menuBar = ApplicationMenu.GetMenu("Open", "Computer");
            menuBar.FindFirstDescendant(cf => cf.ByAutomationId("OpenFile")).AsButton().Invoke();
            Helpers.WaitUntilInputIsProcessed();
            //MainWindow.WaitWhileBusy();
            FileDialog.Attach(MainWindow).Open(fileToLoad);
            WaitWhileBusy(TimeSpan.FromMinutes(5));
        }
        private static string GetFileToLoad(string layoutFile)
        {
            string fileToLoad = layoutFile;
            if (!Path.IsPathRooted(fileToLoad))
            {
                string envPath = Environment.GetEnvironmentVariable("FLDT_LAYOUT_PATH");
                if (envPath == null)
                {
                    fileToLoad = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Layouts", layoutFile);
                    if (!File.Exists(fileToLoad))
                    {
                        fileToLoad = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "Layouts", layoutFile);
                    }
                }
                else
                {
                    fileToLoad = Path.Combine(envPath.Replace("\"", string.Empty), layoutFile);
                }
            }
            if (!File.Exists(fileToLoad)) throw new FileNotFoundException("Layout file could not be found", fileToLoad);
            return fileToLoad;
        }

        public void SaveLayout(string fileToSave, bool overwrite = false)
        {
            if (!fileToSave.ToLower().EndsWith(".vcmx")) throw new InvalidOperationException($"File extension when saving layout file must be 'vcmx' and not '{Path.GetExtension(fileToSave)}'");
            AutomationElement menuBar = ApplicationMenu.GetMenu("Save As", "Computer");
            menuBar.FindFirstDescendant(cf => cf.ByAutomationId("OpenFile")).AsButton().Invoke();
            Helpers.WaitUntilInputIsProcessed();
            MainWindow.WaitWhileBusy();
            FileDialog.Attach(MainWindow).Save(fileToSave, overwrite);
            WaitWhileBusy(TimeSpan.FromMinutes(1));
        }

        public static VcEngine Attach()
        {
            Process process = Process.GetProcessesByName("VisualComponents.Essentials").Concat(Process.GetProcessesByName("VisualComponents.Engine")).FirstOrDefault();
            if (process == null)
            {
                throw new Exception("No process could be attached");
            }
            Application application = Application.Attach(process.Id);
            var automation = new UIA3Automation();
            return new VcEngine(application, automation);
        }

        public static VcEngine AttachOrLaunch()
        {
            return AttachOrLaunch(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Visual Components", "Visual Components Professional"));
        }
        public static VcEngine AttachOrLaunch(string installationPath)
        {
            return AttachOrLaunch(new ProcessStartInfo()
            {
                WorkingDirectory = installationPath,
                FileName = Path.Combine(installationPath, "VisualComponents.Engine.exe"),
                Arguments = "-automation-mode",
                WindowStyle = ProcessWindowStyle.Maximized
            });
        }
        public static VcEngine AttachOrLaunch(ProcessStartInfo processStartInfo)
        {
            Process process = Process.GetProcessesByName("VisualComponents.Essentials").Concat(Process.GetProcessesByName("VisualComponents.Engine")).FirstOrDefault();
            if (process == null)
            {
                FlaUI.Core.Application.Launch(processStartInfo);
            }
            return Attach();
        }
    }
}