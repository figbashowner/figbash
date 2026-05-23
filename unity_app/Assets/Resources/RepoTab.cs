using SFB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Properties;
using Unity.VisualScripting;

using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Assets
{

    [UxmlElement]
    public partial class RepoTab : VisualElement
    {
        private MultiColumnListView list;
        private Button _importButton;

        public RepoTab()
        {
//            UnityEngine.Debug.Log($"ImportTab constructor");

            RegisterCallback<AttachToPanelEvent>(e =>
            { /* do something here when element is added to UI */
                VisualTreeAsset uiAsset = Resources.Load<VisualTreeAsset>("RepoTab");
                uiAsset.CloneTree(this);

                _importButton = this.Q<Button>("ImportButton");
                _importButton.RegisterCallback<ClickEvent>(HandleImportButtonClick);
                DataManager.Instance.OnDataChanged += OnDataChanged;

            });
            RegisterCallback<DetachFromPanelEvent>(e =>
            { /* do something here when element is removed from UI */
                //int tmp = 4;
            });

        }

        private void List_OnSelectionChanged(StlFile stl)
        {
        }

        private void HandleImportButtonClick(ClickEvent evt)
        {
            var p = this.parent;
            while (p.parent != null) 
            {
                p = p.parent;
            }
            PromptDialog.Show(p, "Enter Repo URL", "Enter the URL of the desired repository", "", (url) =>
            {
                using (UnityWebRequest www = UnityWebRequest.Get(url))
                {
                    www.SendWebRequest();
                    while (www.result == UnityWebRequest.Result.InProgress)
                    {
                    }

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        DataManager.Instance.AddRepo(url, www.downloadHandler.text);
                    }
                    else
                    {

                    }
                }
            });
        }

                
        public void SetSelectedImport(StlFile stl)
        {
        }

        private void OnDataChanged()
        {

        }
    }
}
