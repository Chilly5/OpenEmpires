using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private GameObject localModelsCard;private TMP_Text localModelDescription,localModelStatus,localModelChoice;
        private TMP_InputField localModelImport;private Button localModelCancel;
        private IReadOnlyList<WhisperTrustedModel> localModelCatalog;
        private string selectedLocalModel;private CommanderLocalModelProvisioner localModelProvisioner;
        private CancellationTokenSource localModelCancellation;private int localModelGeneration;
        public bool SelectLocalModelCandidate(string file)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return false;
#else
            if(localModelCatalog==null)localModelCatalog=WhisperModelCatalog.Load();
            foreach(var entry in localModelCatalog)if(entry.FileName==file)
            {
                CancelLocalModelWork();selectedLocalModel=file;
                if(localModelChoice!=null)localModelChoice.text="Model: "+file.Replace("ggml-","").Replace(".bin","")+" (cycle)";
                if(localModelDescription!=null)localModelDescription.text=(entry.Bytes/(1024d*1024d)).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" MiB; "+(entry.Language=="en"?"English only":"multilingual")+". Candidate — quality not measured. Download/import does not select or record.";
                if(localModelStatus!=null)localModelStatus.text="Active preference: "+CommanderVoiceSettings.ModelName+". Choose Use verified separately.";
                return true;
            }
            return false;
#endif
        }
        private void CycleLocalModel()
        {
            if(localModelCatalog==null||localModelCatalog.Count==0)return;int index=0;
            for(int i=0;i<localModelCatalog.Count;i++)if(localModelCatalog[i].FileName==selectedLocalModel){index=(i+1)%localModelCatalog.Count;break;}
            SelectLocalModelCandidate(localModelCatalog[index].FileName);
        }
        private void BuildLocalModelUI()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            localModelsCard=UIObject("LocalModels",voiceSetupCard.transform);PanelElement(localModelsCard,176);
            var layout=localModelsCard.AddComponent<VerticalLayoutGroup>();layout.spacing=4;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var choice=CompactButton(localModelsCard.transform,"Candidate","Model: choose",CycleLocalModel);PanelElement(choice.gameObject,24);localModelChoice=choice.GetComponentInChildren<TMP_Text>();
            localModelDescription=Text("Description",localModelsCard.transform,"",11,TextAlignmentOptions.Left);localModelDescription.richText=false;PanelElement(localModelDescription.gameObject,38);
            localModelImport=VoiceSetupInput("LocalModelFile","Full path to downloaded model .bin (manual import)",false);localModelImport.transform.SetParent(localModelsCard.transform,false);PanelElement(localModelImport.gameObject,26);
            var download=CompactButton(localModelsCard.transform,"Download","Download",()=>{_=ProvisionLocalModelAsync(false,false);});
            var import=CompactButton(localModelsCard.transform,"Import","Import file",()=>{_=ProvisionLocalModelAsync(true,false);});
            var use=CompactButton(localModelsCard.transform,"UseVerified","Use verified",()=>{_=ProvisionLocalModelAsync(false,true);});
            PanelRow("ModelActions",localModelsCard.transform,download.gameObject,import.gameObject,use.gameObject);
            localModelStatus=Text("Status",localModelsCard.transform,"",11,TextAlignmentOptions.Left);localModelStatus.richText=false;PanelElement(localModelStatus.gameObject,28);
            localModelCancel=CompactButton(localModelsCard.transform,"Cancel","Cancel model operation",CancelLocalModelWork);PanelElement(localModelCancel.gameObject,24);localModelCancel.gameObject.SetActive(false);
            localModelCatalog=WhisperModelCatalog.Load();SelectLocalModelCandidate(CommanderVoiceSettings.ModelName);
            if(selectedLocalModel==null&&localModelCatalog.Count>0)SelectLocalModelCandidate(localModelCatalog[0].FileName);
            voiceSetupCard.GetComponent<LayoutElement>().preferredHeight=532;voiceSetupCard.GetComponent<LayoutElement>().minHeight=532;
#endif
        }
        private void CancelLocalModelWork()
        {
            localModelGeneration++;localModelCancellation?.Cancel();localModelProvisioner?.Dispose();localModelProvisioner=null;
            if(localModelCancel!=null)localModelCancel.gameObject.SetActive(false);
        }
        private void UpdateLocalModelProgress()
        {
            if(localModelCancellation==null||localModelCancellation.IsCancellationRequested||localModelProvisioner==null||localModelStatus==null)return;
            long maximum=localModelProvisioner.TotalBytes,done=localModelProvisioner.ReceivedBytes;
            localModelStatus.text="Verifying/installing: "+(done/(1024d*1024d)).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" / "+(maximum/(1024d*1024d)).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" MiB. No recording, upload or model activation.";
        }
        private async Task<bool> ProvisionLocalModelAsync(bool import,bool activate)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            await Task.CompletedTask;return false;
#else
            if(string.IsNullOrEmpty(selectedLocalModel)||localModelCatalog==null)return false;
            CancelLocalModelWork();int generation=localModelGeneration,runtime=runtimeGeneration;string selected=selectedLocalModel;
            string path=localModelImport?.text;
            if(import&&(string.IsNullOrWhiteSpace(path)||path.Length>2048)){if(localModelStatus!=null)localModelStatus.text="Enter a bounded .bin file path; no file was imported.";return false;}
            var cancellation=new CancellationTokenSource();localModelCancellation=cancellation;
            var service=new CommanderLocalModelProvisioner(WhisperModelLocator.WritableModelRoot,localModelCatalog);localModelProvisioner=service;
            if(localModelCancel!=null)localModelCancel.gameObject.SetActive(true);
            try
            {
                var result=await(activate?service.VerifyAsync(selected,cancellation.Token):import?service.ImportAsync(selected,path,cancellation.Token):service.DownloadAsync(selected,cancellation.Token));
                if(cancellation.IsCancellationRequested||generation!=localModelGeneration||runtime!=runtimeGeneration||destroyCleanupComplete)return false;
                if(!result.Success){if(localModelStatus!=null)localModelStatus.text=LocalModelFailureMessage(result.Code);return false;}
                if(!activate){if(localModelStatus!=null)localModelStatus.text="Installed and checksum verified. Not selected: use Use verified explicitly. No recognition-quality claim.";return true;}
                WhisperTrustedModel chosen=null;foreach(var entry in localModelCatalog)if(entry.FileName==selected)chosen=entry;
                if(chosen?.Language=="en"&&CommanderVoiceSettings.Language!="en"){if(localModelStatus!=null)localModelStatus.text="This model is English-only. Keep the multilingual model or select English before activation.";return false;}
                CommanderVoiceSettings.ModelName=selected;CommanderVoiceSettings.VoiceEnabled=true;
                SetVoiceProvider(new WhisperCommanderSpeechToTextProvider(selected,CommanderVoiceSettings.Language));
                if(localModelStatus!=null)localModelStatus.text="Verified local model selected explicitly. Quality remains unmeasured; recording still needs a fresh action.";
                return true;
            }
            catch(Exception){if(generation==localModelGeneration&&runtime==runtimeGeneration&&!destroyCleanupComplete&&localModelStatus!=null)localModelStatus.text="Model setup failed safely. Existing models/text remain available; check the file, space and network.";return false;}
            finally
            {
                service.Dispose();if(ReferenceEquals(localModelProvisioner,service))localModelProvisioner=null;
                if(ReferenceEquals(localModelCancellation,cancellation)){localModelCancellation=null;if(localModelCancel!=null)localModelCancel.gameObject.SetActive(false);}
                cancellation.Dispose();
            }
#endif
        }
        private static string LocalModelFailureMessage(string code)
        {
            switch(code)
            {
                case "MODEL_BUSY":return "An earlier model operation is still finishing. Wait before starting another; no replacement was queued.";
                case "MODEL_CANCELLED":return "Model operation cancelled. No model was activated.";
                case "MODEL_MISSING":return "Model is missing. Import its exact verified .bin or use Download, then Use verified.";
                case "MODEL_HASH_MISMATCH":case "MODEL_SIZE_MISMATCH":return "Model checksum/size is wrong. It was not activated or overwritten. Use the pinned file; inspect/remove a corrupt cache copy separately.";
                case "MODEL_DISK_FULL":case "MODEL_CACHE_LIMIT":return "Model storage is full or at its limit. Free model-cache/disk space before a fresh operation.";
                case "MODEL_NETWORK_FAILED":case "MODEL_DOWNLOAD_FAILED":return "Pinned model download unavailable. Retry deliberately or import the pinned file; no fallback/model switch occurred.";
                default:return "Model setup failed safely. Check the file/path/storage. Typing and existing models remain available.";
            }
        }
    }
}
