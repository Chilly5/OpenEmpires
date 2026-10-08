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
        private enum SemanticSetupMode { Existing, SessionKey, Gateway, Disabled }
        private SemanticSetupMode semanticSetupMode;
        private GameObject semanticSetupCard;
        private TMP_Text semanticSetupDisclosure;
        private TMP_InputField semanticKeyField, semanticGatewayField, semanticSessionField;
        private bool semanticSetupVisible;
        private string semanticSetupError;

        public string SemanticSetupStatus
        {
            get
            {
                if (!string.IsNullOrEmpty(semanticSetupError)) return semanticSetupError;
                if (semanticSetupMode == SemanticSetupMode.Disabled)
                    return "Text AI translation disabled. Already approved work continues. Configure Text AI to interpret new requests; grounded local questions and task controls remain usable.";
                if (semanticProvider is OpenRouterCommanderProvider router)
                {
                    string route = router.UsesGateway ? "Luna gateway" : "OpenRouter Luna";
                    if (!router.HasConfiguration || router.UsesGateway && !router.HasGatewayConfiguration)
                        return route + " setup required. Use Text AI settings; do not enter a speech service key as a semantic key.";
                    if (router.LastRequestTimedOut) return route + " timed out. Check the connection/service, then deliberately retry; no automatic retry or model switch.";
                    if (router.LastNetworkFailure) return route + " connection failed. Check your connection and gateway configuration, then retry.";
                    switch (router.LastHttpStatusCode)
                    {
                        case 401: return route + " authentication failed. Replace the key or obtain a fresh gateway service session. No order was submitted.";
                        case 402: return route + " has insufficient credits. Check the operator/account budget; setup does not purchase credits.";
                        case 403: return route + " access forbidden. Ask the operator to check session/model access.";
                        case 404: return route + " model unavailable. Ask the operator to check the configured Luna model; no model was substituted.";
                        case 429: return route + " is busy or rate-limited. Wait before trying again; no automatic retry.";
                    }
                    if (router.LastHttpStatusCode >= 500) return route + " service unavailable. Check gateway/provider setup and try again later.";
                    if (router.HasValidatedSemanticResponse) return route + " responded with validated semantic data. Gameplay still uses normal validation and separate plan approval.";
                    if (router.HasAttemptedRequest) return route + " request did not produce a validated interpretation. No connection-success or gameplay-success claim; check the request message.";
                    return route + " configured for this session; availability is checked on your first request. Settings alone send nothing.";
                }
                return adapter == null ? "Commander runtime is not ready yet."
                    : "Existing translator selected. Local demo/offline support is limited; configure Text AI for OpenRouter Luna or an authenticated gateway.";
            }
        }

        public void ShowSemanticSetup()
        {
            HideVoiceSetup(); MinimizeCommander(); semanticSetupVisible = true;
            if (semanticSetupCard != null) semanticSetupCard.SetActive(true);
            Canvas.ForceUpdateCanvases();UpdatePresentation();Canvas.ForceUpdateCanvases();
            RefreshSemanticSetupStatus();
        }
        public void HideSemanticSetup()
        {
            semanticSetupVisible = false;
            foreach (var field in new[] { semanticKeyField, semanticSessionField })
                if (field != null) { field.text = ""; field.DeactivateInputField(); }
            semanticGatewayField?.DeactivateInputField();
            if (semanticSetupCard != null) semanticSetupCard.SetActive(false);
        }
        private void ObserveSemanticProviderSetup(ICommanderAIProvider provider)
        {
            if (gatewaySemanticTransport != null && (!(provider is OpenRouterCommanderProvider router)
                || !router.UsesTransport(gatewaySemanticTransport)))
            { gatewaySemanticTransport.Dispose(); gatewaySemanticTransport = null; }
            semanticSetupMode = SemanticSetupMode.Existing; semanticSetupError = null;
            HideSemanticSetup(); RefreshSemanticSetupStatus();
        }
        private void RefreshSemanticSetupStatus()
        {
            if (semanticSetupDisclosure != null)
                semanticSetupDisclosure.text = SemanticSetupStatus + "\n\nText AI sends typed/reviewed text and bounded owned game context to OpenRouter Luna (via the selected gateway when used). This is NOT microphone permission, audio processing consent, transcript Send or plan approval. No reusable key is saved. Closing clears unapplied secrets; Disable forgets this runtime's override, not external developer configuration.";
        }
        private void BeginSemanticSetupRequest() { semanticSetupError = null; RefreshSemanticSetupStatus(); }
        private Task<CommanderSemanticResult> RequestSemanticTranslation(ICommanderSemanticProvider provider,
            CommanderSemanticProviderRequest request, CancellationToken token)
        { BeginSemanticSetupRequest(); return provider.TranslateSemanticAsync(request, token); }
        private bool SetupFailure(string safeMessage)
        { semanticSetupError = safeMessage; RefreshSemanticSetupStatus(); return false; }

        public bool ConfigureSessionOpenRouter(string key, ICommanderHttpTransport transport = null)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return SetupFailure("Direct reusable key entry is unavailable in Web. Use an operator-supplied authenticated gateway session; browser credentials are never persisted.");
#else
            if (destroyCleanupComplete || semanticSimulation == null || semanticGoalManager == null || semanticDispatcher == null)
                return SetupFailure("Wait for the Commander runtime before configuring a key.");
            if (key == null || key.Length < 8 || key.Length > 512)
                return SetupFailure("Enter a bounded OpenRouter semantic key, not a speech API key. No validation request was sent.");
            foreach (char ch in key)
                if (ch < 33 || ch > 126) return SetupFailure("The key contains whitespace or invalid characters. No value was saved or sent.");
            SwitchSemanticProvider(new OpenRouterCommanderProvider(key, transport), SemanticSetupMode.SessionKey);
            return true;
#endif
        }
        public bool ConfigureSemanticGateway(string gateway, Func<string> sessionTokenSource,
            ICommanderGatewayTransport connection = null)
        {
            if (destroyCleanupComplete || semanticSimulation == null || semanticGoalManager == null || semanticDispatcher == null)
                return SetupFailure("Wait for the Commander runtime before configuring the gateway.");
            if (gateway == null || gateway.Length > 1024 || !Uri.TryCreate(gateway, UriKind.Absolute, out var uri)
                || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/"
                || uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
                return SetupFailure("Use the trusted operator's HTTPS gateway origin (localhost HTTP is development only), without credentials, path or query.");
            string token; try { token = sessionTokenSource?.Invoke(); } catch { token = null; }
            if (token == null || token.Length != 36 || !Guid.TryParseExact(token, "D", out _))
                return SetupFailure("Enter the operator-issued service session UUID, not a reusable provider API key. Multiplayer sign-in is not required. Settings send no request.");
            var transport = new CommanderGatewaySemanticTransport(uri.GetLeftPart(UriPartial.Authority), sessionTokenSource, connection);
            SwitchSemanticProvider(new OpenRouterCommanderProvider("gateway-session", transport), SemanticSetupMode.Gateway);
            gatewaySemanticTransport = transport;
            return true;
        }
        private void SwitchSemanticProvider(OpenRouterCommanderProvider provider, SemanticSetupMode mode)
        {
            var pipeline = strategicPipeline;
            Initialize(provider, semanticSimulation, semanticGoalManager, semanticDispatcher);
            if (pipeline != null) InitializeStrategic(provider, pipeline);
            semanticSetupMode = mode; semanticSetupError = null;
            ShowSemanticSetup();
        }
        public void DisableSemanticTranslation()
        {
            if (destroyCleanupComplete || semanticSimulation == null || semanticGoalManager == null || semanticDispatcher == null) return;
            // Empty explicit configuration never reads developer keys or sends HTTP.
            SwitchSemanticProvider(new OpenRouterCommanderProvider(""), SemanticSetupMode.Disabled);
        }
        private void ApplySessionKeyFromUI() => ConfigureSessionOpenRouter(semanticKeyField?.text);
        private void ApplySemanticGatewayFromUI()
        {
            string manual = semanticSessionField?.text ?? "";
            Func<string> source = manual.Length == 0 ? () => MatchmakingManager.Instance?.AuthToken ?? "" : () => manual;
            ConfigureSemanticGateway(semanticGatewayField?.text, source);
        }
        private void BuildSemanticSetupUI()
        {
            semanticSetupCard = ReviewCard("SemanticSetup", "Text AI — separate from voice and approval", out semanticSetupDisclosure,
                new[] { "Use key", "Use gateway", "Disable" },
                new UnityEngine.Events.UnityAction[] { ApplySessionKeyFromUI, ApplySemanticGatewayFromUI, DisableSemanticTranslation });
            semanticKeyField = VoiceSetupInput("SessionKey", "OpenRouter semantic key — session only", true, semanticSetupCard.transform);
            semanticKeyField.characterLimit = 512; semanticKeyField.transform.SetSiblingIndex(1);
            semanticGatewayField = VoiceSetupInput("SemanticGateway", "Trusted gateway HTTPS origin", false, semanticSetupCard.transform);
            semanticGatewayField.characterLimit = 1024; semanticGatewayField.transform.SetSiblingIndex(2);
            semanticSessionField = VoiceSetupInput("SemanticSession", "Operator-issued service session UUID", true, semanticSetupCard.transform);
            semanticSessionField.characterLimit = 36; semanticSessionField.transform.SetSiblingIndex(3);
            PanelElement(CompactButton(semanticSetupCard.transform, "Close", "Close settings", HideSemanticSetup).gameObject, 32);
            semanticSetupCard.GetComponent<LayoutElement>().preferredHeight = 336;
            semanticSetupCard.GetComponent<LayoutElement>().minHeight = 336;
#if UNITY_WEBGL && !UNITY_EDITOR
            semanticKeyField.gameObject.SetActive(false);
            semanticSetupCard.transform.Find("Actions/Action0").gameObject.SetActive(false);
#endif
            MakeSetupScrollable(semanticSetupCard);semanticSetupCard.SetActive(false); RefreshSemanticSetupStatus();
        }
    }
}
