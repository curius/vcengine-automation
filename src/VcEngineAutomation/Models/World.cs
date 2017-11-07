﻿using System;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using System.Linq;
using System.Text.RegularExpressions;
using VcEngineAutomation.Windows;

namespace VcEngineAutomation.Models
{
    public class World
    {
        private readonly VcEngine vcEngine;
        private static readonly Regex MsgBoxRegex = new Regex(@"Component '(.*)' \(");

        public World(VcEngine vcEngine)
        {
            this.vcEngine = vcEngine;
        }

        public void Clear()
        {
            vcEngine.ApplicationMenu.GetMenu("Clear All");
            Wait.UntilInputIsProcessed();
            VcMessageBox.AttachIfShown(vcEngine)?.ClickNo();
        }

        public void SelectAll()
        {
            vcEngine.MoveFocusTo3DViewPort();
            Keyboard.Press(VirtualKeyShort.CONTROL);
            Keyboard.Type(VirtualKeyShort.KEY_A);
            Keyboard.Release(VirtualKeyShort.CONTROL);
        }

        public void LoadComponentByVcid(string vcid)
        {
            vcEngine.ECataloguePanel.SearchTextBox.Enter(vcid);
            Wait.UntilInputIsProcessed();
            Wait.UntilResponsive(vcEngine.ECataloguePanel.SearchTextBox);
            vcEngine.ECataloguePanel.DisplayedItems.First().DoubleClick();
        }

        public int SelectedItemsCount
        {
            get
            {
                string header = vcEngine.PropertiesPanel.Header;
                if (header.Contains("Objects Selected"))
                {
                    return int.Parse(header.Replace("Objects Selected", "").Trim());
                }
                return string.IsNullOrWhiteSpace(header) ? 0 : 1;
            }
        }

        public void RenameSelectedComponent(string newComponentName)
        {
            vcEngine.PropertiesPanel.SetProperty("Name", newComponentName);
        }

        public void CopyAndPasteSelectedComponents(TimeSpan? waitTimeSpan = null)
        {
            vcEngine.MoveFocusTo3DViewPort();
            vcEngine.Ribbon.HomeTab.ClickButton("Clipboard", "Copy");
            vcEngine.Ribbon.HomeTab.ClickButton("Clipboard", "Paste", waitTimeSpan);
        }
        public void DeleteSelectedComponent()
        {
            vcEngine.MoveFocusTo3DViewPort();
            vcEngine.Ribbon.HomeTab.ClickButton("Clipboard", "Delete");
        }
    }
}
