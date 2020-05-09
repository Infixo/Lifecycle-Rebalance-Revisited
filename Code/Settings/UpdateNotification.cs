﻿using System;
using UnityEngine;
using ColossalFramework.UI;


namespace LifecycleRebalance
{
    /// <summary>
    /// Base class of the update notification panel.
    /// </summary>
    public class UpdateNotification : UIPanel
    {
        // Constants.
        private const float panelWidth = 600;
        private const float panelHeight = 300;
        private const float spacing = 10;

        // Instance references.
        private static GameObject uiGameObject;
        private static UpdateNotification _instance;
        public static UpdateNotification instance { get { return _instance; } }


        /// <summary>
        /// Creates the panel object in-game.
        /// </summary>
        public void Create()
        {
            try
            {
                // Destroy existing (if any) instances.
                uiGameObject = GameObject.Find("LifecycleRebalanceUpgradeNotification");
                if (uiGameObject != null)
                {
                    UnityEngine.Debug.Log("Lifecycle Rebalance Revisited: found existing upgrade notification instance.");
                    GameObject.Destroy(uiGameObject);
                }

                // Create new instance.
                // Give it a unique name for easy finding with ModTools.
                uiGameObject = new GameObject("LifecycleRebalanceUpgradeNotification");
                uiGameObject.transform.parent = UIView.GetAView().transform;
                _instance = uiGameObject.AddComponent<UpdateNotification>();
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }


        /// <summary>
        /// Create the update notification panel; called by Unity just before any of the Update methods is called for the first time.
        /// </summary>
        public override void Start()
        {
            base.Start();

            try
            {
                // Basic setup.
                isVisible = true;
                canFocus = true;
                isInteractive = true;
                width = panelWidth;
                height = panelHeight;
                relativePosition = new Vector3(Mathf.Floor((GetUIView().fixedWidth - width) / 2), Mathf.Floor((GetUIView().fixedHeight - height) / 2));
                backgroundSprite = "UnlockingPanel2";

                // Title.
                AddText("Lifecycle Rebalance Revisited 1.3.5 update", spacing, spacing, 1.0f);

                // Note 1.

                float currentX = AddText("Lifecycle Rebalance Revisited has been updated to version 1.3.5.  This update expands the options panel to enable easy changing of the following configuration settings:", spacing, 40);

                // Note 2.
                currentX = AddText("The percentage of dead bodies that require transport(set to zero to remove the need for deathcare completely).\r\n\r\nThe chance of citizens randomly becoming ill (per decade of life) - note that this does not affect illness from other factors, such as pollution or noise.", spacing * 2, currentX + 20);

                // Note 3.

                currentX = AddText("These changes only provide the ability to change the existing configuration without having to manually edit the XML file; no calculations have been changed in this update and all mod behaviour remains the same.", spacing, currentX + 20);

                // Auto resize panel to accomodate note.
                this.height = currentX + 60;

                // Close button.
                UIButton closeButton = CreateButton(this);
                closeButton.relativePosition = new Vector3(spacing, this.height - closeButton.height - spacing);
                closeButton.text = "Close";
                closeButton.Enable();

                // Event handler.
                closeButton.eventClick += (c, p) =>
                {
                    // Just hide this panel and destroy the game object - nothing more to do this load.
                    this.Hide();
                    GameObject.Destroy(uiGameObject);
                };

                // "Don't show again" button.
                UIButton noShowButton = CreateButton(this);
                noShowButton.relativePosition = new Vector3(this.width - noShowButton.width - spacing, this.height - closeButton.height - spacing);
                noShowButton.text = "Don't show again";
                noShowButton.Enable();

                // Event handler.
                noShowButton.eventClick += (c, p) =>
                {
                    // Update and save settings file.
                    Loading.settingsFile.NotificationVersion = 1;
                    Configuration<SettingsFile>.Save();

                    // Just hide this panel and destroy the game object - nothing more to do.
                    this.Hide();
                    GameObject.Destroy(uiGameObject);
                };
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }


        private float AddText(string text, float x, float y, float size = 0.8f)
        {
            // Note 1.
            UILabel textLabel = this.AddUIComponent<UILabel>();
            textLabel.relativePosition = new Vector3(x, y);
            textLabel.textAlignment = UIHorizontalAlignment.Left;
            textLabel.text = text;
            textLabel.textScale = size;
            textLabel.autoSize = false;
            textLabel.autoHeight = true;
            textLabel.width = this.width - (x * 2);
            textLabel.wordWrap = true;

            return textLabel.height + y;
        }



        private UIButton CreateButton(UIComponent parent)
        {
            UIButton button = parent.AddUIComponent<UIButton>();

            button.size = new Vector2(200f, 30f);
            button.textScale = 0.9f;
            button.normalBgSprite = "ButtonMenu";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.disabledTextColor = new Color32(128, 128, 128, 255);
            button.canFocus = false;

            return button;
        }
    }
}