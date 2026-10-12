using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OpenEmpires
{
    public sealed partial class CommanderChatUI
    {
        private GameObject voiceSetupCard;
        private TMP_Text voiceSetupDisclosure;
        private TMP_InputField voiceGatewayField,voiceSessionField;
        private Button onlineConsentButton;
        private CommanderGatewaySpeechToTextProvider pendingOnlineVoice;
        private CancellationTokenSource voiceSetupCancellation;
        private CancellationTokenSource microphoneActivationCancellation;
        private int voiceSetupGeneration;
        private bool voiceSetupVisible;
        private string reviewedGateway;private Func<string> reviewedGatewaySession;
        private CommanderGatewaySemanticTransport gatewaySemanticTransport;

        public void ShowVoiceSetup(){HideSemanticSetup();MinimizeCommander();voiceSetupVisible=true;if(voiceSetupCard!=null)voiceSetupCard.SetActive(true);Canvas.ForceUpdateCanvases();UpdatePresentation();Canvas.ForceUpdateCanvases();}
        public void HideVoiceSetup(){voiceSetupVisible=false;CancelPendingVoiceSetup();if(voiceSetupCard!=null)voiceSetupCard.SetActive(false);}
        private void CancelPendingVoiceSetup(){voiceSetupGeneration++;voiceSetupCancellation?.Cancel();microphoneActivationCancellation?.Cancel();pendingOnlineVoice?.Dispose();pendingOnlineVoice=null;
            CancelLocalModelWork();
            if(onlineConsentButton!=null)onlineConsentButton.interactable=false;}
        private void VoiceSetupMessage(string value){if(voiceSetupDisclosure!=null)voiceSetupDisclosure.text=value;}
        public async Task<bool> ActivateMicrophoneAsync()
        {
            ShowVoiceSetup();microphoneActivationCancellation?.Cancel();int generation=voiceSetupGeneration;
            var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(15));microphoneActivationCancellation=cancellation;
            void Message(string value)=>VoiceSetupMessage(value+(pendingOnlineVoice!=null?"\n\n"+pendingOnlineVoice.Disclosure:""));
            Message("Microphone permission only — no recording or audio upload. A fresh Record/hold-key action is required after readiness.");
            try{
                bool ready=voiceAudioCapture is ICommanderCaptureReadiness readiness?await readiness.RequestPermissionAsync(cancellation.Token):true;
                if(cancellation.IsCancellationRequested||generation!=voiceSetupGeneration||destroyCleanupComplete)return false;
                Message(ready?"Microphone ready. Use a NEW Record/hold-key action. Online audio still requires explicit processing consent."
                    :"Microphone unavailable/permission denied. Check browser permission, HTTPS and microphone embedding policy; typing remains usable.");
                return ready;
            }catch(OperationCanceledException){return false;}
            catch(Exception){if(generation==voiceSetupGeneration&&!destroyCleanupComplete)Message("Microphone activation failed. No recording started; check permission/device and try a fresh action.");return false;}
            finally{if(ReferenceEquals(microphoneActivationCancellation,cancellation))microphoneActivationCancellation=null;cancellation.Dispose();}
        }

        public async Task<bool> PrepareOnlineVoiceAsync(string gateway,Func<string> sessionTokenSource,ICommanderGatewayTransport transport=null)
        {
            ShowVoiceSetup();CancelPendingVoiceSetup();int generation=voiceSetupGeneration;
            var cancellation=new CancellationTokenSource(TimeSpan.FromSeconds(15));voiceSetupCancellation=cancellation;
            var connection=transport??new CommanderGatewayTransport();bool transferred=false;
            VoiceSetupMessage("Loading processing policy only — no microphone or audio upload. Consent has not been granted.");
            try{
                var response=await connection.SendAsync(gateway,Guid.NewGuid(),"","","en",Array.Empty<byte>(),CommanderGatewayRequestKind.Policy,cancellation.Token);
                if(cancellation.IsCancellationRequested||generation!=voiceSetupGeneration||destroyCleanupComplete)return false;
                if(response==null||response.Status!=200||response.ErrorCode.Length!=0){VoiceSetupMessage("Gateway unavailable. Check its HTTPS origin and operator setup. Typing/on-device remain available.");return false;}
                var candidate=new CommanderGatewaySpeechToTextProvider(gateway,sessionTokenSource,response.Body,connection,CommanderVoiceSettings.Language);
                pendingOnlineVoice=candidate;transferred=true;
                reviewedGateway=gateway;reviewedGatewaySession=sessionTokenSource;
                VoiceSetupMessage(candidate.Disclosure+"\n\nNOT selected. Choose Allow online explicitly, or decline with On device/Text only. Nothing will record automatically.");
                if(onlineConsentButton!=null)onlineConsentButton.interactable=true;
                if(voiceSessionField!=null)voiceSessionField.text=""; // Session credential stays in memory only, never in prefs/logs.
                return true;
            }catch(OperationCanceledException){return false;}
            catch(Exception){if(generation==voiceSetupGeneration&&!destroyCleanupComplete)VoiceSetupMessage("Invalid gateway policy or session setup. No consent, recording or upload occurred. Ask the operator to verify configuration.");return false;}
            finally{if(!transferred)connection.Dispose();if(ReferenceEquals(voiceSetupCancellation,cancellation))voiceSetupCancellation=null;cancellation.Dispose();}
        }
        public bool AcceptOnlineVoiceConsent(ICommanderAudioCapture capture=null)
        {
            var candidate=pendingOnlineVoice;if(candidate==null||!candidate.ConsentToOnlineAudio()){
                VoiceSetupMessage("An operator-issued service session is required. It is NOT a transcription/provider API key or multiplayer login. Enter a valid session and review gateway policy again.");return false;}
            pendingOnlineVoice=null;CommanderVoiceSettings.VoiceEnabled=true;
            SetVoiceProvider(candidate,capture);HideVoiceSetup();UpdateVoiceUI();return true;
        }
        public void UseOnDeviceVoice()
        {
            HideVoiceSetup();(voiceProvider as CommanderGatewaySpeechToTextProvider)?.RevokeConsent();
#if UNITY_WEBGL && !UNITY_EDITOR
            voiceController?.Cancel();CommanderVoiceSettings.VoiceEnabled=false;
            VoiceSetupMessage("Browser-local recognition is not configured yet. Text input remains available; no online upload is allowed.");
#else
            CommanderVoiceSettings.VoiceEnabled=true;SetVoiceProvider(new WhisperCommanderSpeechToTextProvider(CommanderVoiceSettings.ModelName,CommanderVoiceSettings.Language));
#endif
            UpdateVoiceUI();
        }
        public bool EnableGatewaySemanticTranslation(ICommanderGatewayTransport connection=null)
        {
            if(string.IsNullOrWhiteSpace(reviewedGateway)||reviewedGatewaySession==null||semanticSimulation==null||semanticGoalManager==null){
                VoiceSetupMessage("Load the gateway policy/session first. Enabling semantic translation sends reviewed/typed text and bounded owned game context to the gateway/OpenRouter Luna. It is separate from microphone/audio consent and plan approval.");return false;}
            return ConfigureSemanticGateway(reviewedGateway,reviewedGatewaySession,connection);
        }
        private void ReviewOnlinePolicyFromUI()
        {
            string gateway=voiceGatewayField?.text??"",manual=voiceSessionField?.text??"";
            if(gateway.Length>1024||manual.Length>36){VoiceSetupMessage("Enter a gateway HTTPS origin and its service session UUID, not a provider API key. No values are saved or uploaded.");return;}
            Func<string> source=!string.IsNullOrWhiteSpace(manual)?()=>manual:()=>MatchmakingManager.Instance?.AuthToken??"";
            _=PrepareOnlineVoiceAsync(gateway,source);
        }
        private void BuildVoiceSetupUI()
        {
            voiceSetupCard=ReviewCard("VoiceSetup","Voice processing — not gameplay",out voiceSetupDisclosure,
                new[]{"Load policy","Allow online","On device"},new UnityEngine.Events.UnityAction[]{ReviewOnlinePolicyFromUI,()=>AcceptOnlineVoiceConsent(),UseOnDeviceVoice});
            voiceSetupCard.GetComponent<LayoutElement>().preferredHeight=320;
            voiceSetupCard.GetComponent<LayoutElement>().minHeight=320;
            voiceGatewayField=VoiceSetupInput("GatewayOrigin","Gateway HTTPS origin (operator supplied)",false);
            voiceSessionField=VoiceSetupInput("BackendSession","Operator-issued service session UUID",true);
            voiceGatewayField.transform.SetSiblingIndex(1);voiceSessionField.transform.SetSiblingIndex(2);
            onlineConsentButton=voiceSetupCard.transform.Find("Actions/Action1").GetComponent<Button>();onlineConsentButton.interactable=false;
            var activate=CompactButton(voiceSetupCard.transform,"ActivateMicrophone","Enable microphone (permission only)",()=>{_ = ActivateMicrophoneAsync();});PanelElement(activate.gameObject,32);
            var semantic=CompactButton(voiceSetupCard.transform,"SemanticGateway","Use Luna gateway for text + owned context",()=>EnableGatewaySemanticTranslation());PanelElement(semantic.gameObject,32);
            voiceSetupCard.GetComponent<LayoutElement>().preferredHeight=352;voiceSetupCard.GetComponent<LayoutElement>().minHeight=352;
            BuildLocalModelUI();
            var close=CompactButton(voiceSetupCard.transform,"Close","×",HideVoiceSetup);
            PanelElement(close.gameObject,24);close.GetComponent<LayoutElement>().ignoreLayout=true;
            SetRect(close.GetComponent<RectTransform>(),Vector2.one,Vector2.one,new Vector2(-32,-28),new Vector2(-8,-4));
            VoiceSetupMessage("Choose on-device Windows or explicit online audio transcription. Microphone permission, audio-upload consent, reviewed text submission and plan approval are separate. Semantic translation may still be remote. No keys are stored in this panel.");
#if UNITY_WEBGL && !UNITY_EDITOR
            voiceSetupCard.transform.Find("Actions/Action2").GetComponentInChildren<TMP_Text>().text="Text only";
#endif
            MakeSetupScrollable(voiceSetupCard);voiceSetupCard.SetActive(false);
        }
        private TMP_InputField VoiceSetupInput(string name,string hint,bool password,Transform parent=null)
        {
            var obj=UIObject(name,parent??voiceSetupCard.transform);PanelElement(obj,32);
            var image=obj.AddComponent<Image>();image.color=new Color(.11f,.13f,.17f,1);
            var field=obj.AddComponent<TMP_InputField>();field.targetGraphic=image;field.characterLimit=0;
            field.lineType=TMP_InputField.LineType.SingleLine;field.contentType=password?TMP_InputField.ContentType.Password:TMP_InputField.ContentType.Standard;
            field.navigation=new Navigation{mode=Navigation.Mode.None};
            var viewport=UIObject("Viewport",obj.transform);SetRect(viewport.GetComponent<RectTransform>(),Vector2.zero,Vector2.one,new Vector2(6,2),new Vector2(-6,-2));
            viewport.AddComponent<RectMask2D>();field.textViewport=viewport.GetComponent<RectTransform>();
            var value=Text("Text",viewport.transform,"",12,TextAlignmentOptions.Left);value.richText=false;
            SetRect(value.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);field.textComponent=value;
            var placeholder=Text("Placeholder",viewport.transform,hint,11,TextAlignmentOptions.Left);placeholder.richText=false;
            placeholder.color=new Color(.7f,.73f,.78f);SetRect(placeholder.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);field.placeholder=placeholder;
            return field;
        }
        private void OnApplicationFocus(bool focused){if(!focused){HideSemanticSetup();if(IsVoiceRecording||IsVoiceTranscribing)voiceController?.ReportCaptureFailure("MIC_FOCUS_LOST");else voiceController?.Cancel();CancelPendingVoiceSetup();}}
        private void OnApplicationPause(bool paused){if(paused){if(IsVoiceRecording||IsVoiceTranscribing)voiceController?.ReportCaptureFailure("MIC_FOCUS_LOST");else voiceController?.Cancel();CancelPendingVoiceSetup();}}
        private void OnDisable(){HideSemanticSetup();voiceController?.Cancel();CancelPendingVoiceSetup();}
    }
}
