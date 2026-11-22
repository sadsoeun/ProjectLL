using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Self-contained, resolution-independent clear overlay; no imported art or tween package required.</summary>
    [DisallowMultipleComponent]
    public sealed class StageClearCelebration : MonoBehaviour
    {
        public const float MinimumDuration = 1.0f;
        private const float ColumnCentre = 0.25f;   // column centre as a fraction of the screen width (left side)
        private const float TitleScale = 0.80f;       // title + emblem scale relative to the centred layout
        private CanvasGroup group;
        private CanvasGroup titleGroup;
        private RectTransform composition;
        private RectTransform titleRect;
        private LocalizedText title;
        private RectTransform resultBackdrop;
        private CanvasGroup backdropGroup;
        private StageClearGraphic ornament;
        private RectTransform host;
        private float elapsed;
        private float duration = MinimumDuration;
        private bool playing;
        private RectTransform results;
        private LocalizedText coinValue,specialValue,recordBadge;
        private RectTransform timeCard;
        private CanvasGroup resultsGroup;
        private bool showingResult;
        private float resultElapsed;
        private System.Action resultReady;
        public const float ResultTransitionDuration = 0.7f;
        // master clear: fireworks layer (behind the title and result panel) and the badge on the panel
        private MasterFireworksGraphic fireworks;
        private RectTransform masterBadge;
        private CanvasGroup masterBadgeGroup;
        private Image masterBadgePlate;
        private bool mastered;
        private float masterElapsed;
        private Text specialValueText;
        private Color specialValueColor;
        public const float MasterBadgeDelay = 0.45f;
        public bool IsMastered => mastered;

        public float Duration => duration;
        public bool IsPlaying => playing;

        public static StageClearCelebration Create(Transform parent)
        {
            var root = NewRect("StageClearCelebration", parent);
            Stretch(root);
            var effect = root.gameObject.AddComponent<StageClearCelebration>();
            effect.Build();
            effect.Hide();
            return effect;
        }

        private void Build()
        {
            if (group != null) return;
            host = (RectTransform)transform;
            group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = true;
            // Input blocker (invisible) + a soft scrim on the left only: the character on the right stays bright.
            var backdrop = NewRect("Backdrop", transform);
            Stretch(backdrop);
            var blocker = backdrop.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0f);
            blocker.raycastTarget = true;
            var scrim = NewRect("LeftScrim", transform);Stretch(scrim);
            scrim.gameObject.AddComponent<ClearResultScrim>().raycastTarget=false;
            resultBackdrop=NewRect("ResultBackdrop",transform);resultBackdrop.sizeDelta=new Vector2(620,800);
            var plate=resultBackdrop.gameObject.AddComponent<Image>();plate.color=new Color(.018f,.023f,.032f,1);plate.raycastTarget=false;
            GoldUiTheme.DressPanel(resultBackdrop);
            backdropGroup=resultBackdrop.gameObject.AddComponent<CanvasGroup>();backdropGroup.blocksRaycasts=false;backdropGroup.alpha=0;

            // Above the scrim, below the title and the (opaque) result panel, so text stays on top.
            var fireworksRect = NewRect("MasterFireworks", transform);
            Stretch(fireworksRect);
            fireworks = fireworksRect.gameObject.AddComponent<MasterFireworksGraphic>();
            fireworks.raycastTarget = false;
            fireworksRect.gameObject.SetActive(false);
            resultBackdrop.SetAsLastSibling();

            composition = NewRect("Composition", transform);
            composition.sizeDelta = new Vector2(1200f, 720f);
            var art = NewRect("GoldFrameAndParticles", composition);
            art.sizeDelta = composition.sizeDelta;
            ornament = art.gameObject.AddComponent<StageClearGraphic>();
            ornament.raycastTarget = false;

            titleRect = NewRect("LocalizedTitle", composition);
            titleRect.sizeDelta = new Vector2(1000f, 160f);
            var label = titleRect.gameObject.AddComponent<Text>();
            label.font = FontProvider.Get(LocalizationTable.DefaultLanguage);
            label.fontSize = 82;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.94f, 0.76f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 32;
            label.resizeTextMaxSize = 82;
            label.supportRichText = false;
            label.raycastTarget = false;
            var shadow = titleRect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.34f, 0.15f, 0.02f, 0.85f);
            shadow.effectDistance = new Vector2(0f, -3f);
            title = titleRect.gameObject.AddComponent<LocalizedText>();
            titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();

            results = NewRect("ClearTimeAndContinue", transform);
            results.sizeDelta = new Vector2(500,430);
            resultsGroup = results.gameObject.AddComponent<CanvasGroup>();
            // The shared backdrop covers both the title and results; no second outer panel.
            // storyboard column: clear time, coins, specials, then the button (placed by ShowResult)
            timeCard=Card("TimeCard",new Vector2(0,138),new Vector2(460,84));
            var caption=UiKit.Label(timeCard,"TimeCaption","ingame.result_time_label",UiKit.BodySize,TextAnchor.MiddleLeft);
            UiKit.Place(caption.transform,0,.5f,24,0,200,76);caption.GetComponent<Text>().color=UiKit.Muted;
            recordBadge=UiKit.Label(timeCard,"RecordBadge","ui.new_record",UiKit.CaptionSize,TextAnchor.MiddleRight);
            UiKit.Place(recordBadge.transform,1,1,-12,14,200,26);recordBadge.GetComponent<Text>().color=GoldUiTheme.Gold;
            recordBadge.gameObject.SetActive(false);
            var coins=Card("CoinCard",new Vector2(0,40),new Vector2(460,84));
            var specials=Card("SpecialCard",new Vector2(0,-58),new Vector2(460,84));
            coinValue=Metric(coins,"ui.result_coins");specialValue=Metric(specials,"ui.result_specials");
            specialValueText=specialValue.GetComponent<Text>();specialValueColor=specialValueText.color;
            BuildMasterBadge();
            fireworks.SetProtected(titleRect,masterBadge);
            results.gameObject.SetActive(false);
        }

        /// <summary>Gold plate straddling the top edge of the result panel, between the title and the time card.</summary>
        private void BuildMasterBadge()
        {
            masterBadge=NewRect("MasterBadge",results);masterBadge.anchoredPosition=new Vector2(0,228);masterBadge.sizeDelta=new Vector2(260,64);
            masterBadgeGroup=masterBadge.gameObject.AddComponent<CanvasGroup>();
            masterBadgePlate=masterBadge.gameObject.AddComponent<Image>();masterBadgePlate.color=new Color(1f,.78f,.30f,1f);masterBadgePlate.raycastTarget=false;
            var frame=GoldUiTheme.Frame(masterBadge);frame.diamond=false;frame.color=new Color(1f,.97f,.82f,.95f);
            var label=UiKit.Label(masterBadge,"Label","ui.result_master",40,TextAnchor.MiddleCenter);
            UiKit.Place(label.transform,.5f,.5f,0,0,240,60);
            var text=label.GetComponent<Text>();text.color=new Color(.24f,.11f,.02f);
            var shadow=text.GetComponent<Shadow>()??text.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(1f,.95f,.75f,.55f);shadow.effectDistance=new Vector2(0,-1.5f);
            masterBadge.gameObject.SetActive(false);
        }

        /// <summary>First-time master clear on the result screen: shows the badge and fires the fireworks.</summary>
        public void SetMastered(bool value, MasterFireworksSettings fireworksSettings = null)
        {
            Build();
            mastered=value;masterElapsed=0;
            masterBadge.gameObject.SetActive(value);
            specialValueText.color=value?GoldUiTheme.Gold:specialValueColor;
            if(value){fireworks.Play(fireworksSettings);SampleMaster(0);}
            else fireworks.Stop();
        }

        private void SampleMaster(float seconds)
        {
            if(!mastered)return;
            float t=Mathf.Clamp01((seconds-MasterBadgeDelay)/.35f);
            float ease=1f-Mathf.Pow(1f-t,3f);
            masterBadgeGroup.alpha=Mathf.SmoothStep(0,1,t);
            // pops in big and settles with a small tilt; a gentle shimmer afterwards
            masterBadge.localScale=Vector3.one*(1f+.6f*(1f-ease));
            masterBadge.localRotation=Quaternion.Euler(0,0,Mathf.Lerp(-12f,-3f,ease));
            float shimmer=.5f+.5f*Mathf.Sin(seconds*3.2f);
            masterBadgePlate.color=Color.Lerp(new Color(1f,.74f,.26f),new Color(1f,.86f,.42f),shimmer);
        }

        /// <summary>Editor verification: the result screen at the given time since it appeared.</summary>
        public void SampleMasterResult(float seconds)
        {
            SampleResult(seconds);
            SampleMaster(seconds);
            if(mastered)fireworks.Sample(seconds);
        }
        private RectTransform Card(string name,Vector2 position,Vector2 size)
        {
            var card=NewRect(name,results);card.anchoredPosition=position;card.sizeDelta=size;
            var plate=card.gameObject.AddComponent<Image>();plate.color=new Color(.065f,.068f,.08f,1);plate.raycastTarget=false;
            var frame=GoldUiTheme.Frame(card);frame.diamond=false;frame.color=new Color(.7f,.58f,.36f,.45f);
            return card;
        }
        private LocalizedText Metric(RectTransform card,string key)
        {
            var caption=UiKit.Label(card,"Caption",key,UiKit.BodySize,TextAnchor.MiddleLeft);
            UiKit.Place(caption.transform,0,.5f,24,0,250,76);caption.GetComponent<Text>().color=UiKit.Muted;
            if(key=="ui.result_specials")
            {
                var icon=NewRect("GoldenLeafIcon",card);UiKit.Place(icon,0,.5f,30,0,32,40);
                var leaf=icon.gameObject.AddComponent<CollectibleSlot>();leaf.raycastTarget=false;leaf.SetFilled(true);
                UiKit.Place(caption.transform,0,.5f,76,0,212,76);
            }
            var value=UiKit.Label(card,"Value",null,UiKit.HeadingSize,TextAnchor.MiddleRight);
            UiKit.Place(value.transform,1,.5f,-24,0,150,76);return value;
        }
        public void SetSummary(int coins,int collected,int total,bool newRecord)
        {Build();coinValue.SetText(coins.ToString());specialValue.SetText(collected+" / "+total);recordBadge.gameObject.SetActive(newRecord);}

        public float Play(float requestedDuration)
        {
            Build();
            duration = Mathf.Max(MinimumDuration, requestedDuration);
            elapsed = 0f;
            showingResult = false;
            resultReady = null;
            results.gameObject.SetActive(false);
            backdropGroup.alpha=0;
            SetMastered(false);
            group.interactable = false;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            title.SetKey("ingame.clear");
            playing = true;
            Sample(0f);
            return duration;
        }

        public void Hide()
        {
            playing = false;
            showingResult = false;
            resultReady = null;
            if (group != null) group.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void OnDisable() { playing = false; showingResult = false; resultReady = null; }

        public void ShowResult(float seconds, LocalizedText timeLabel, Button continueButton, System.Action onReady)
        {
            Build();
            if (!gameObject.activeSelf) Play(duration);
            playing = false;
            elapsed = duration;
            showingResult = true;
            resultElapsed = 0;
            resultReady = onReady;
            results.gameObject.SetActive(true);
            group.interactable = true;
            timeLabel.transform.SetParent(timeCard,false);
            GoldUiTheme.Place((RectTransform)timeLabel.transform, new Vector2(1f,.5f),new Vector2(1f,.5f),new Vector2(-22,0),new Vector2(210,70));
            var label = timeLabel.GetComponent<Text>();
            UiKit.TextStyle(label.transform,44,TextAnchor.MiddleRight);
            label.resizeTextForBestFit=true;label.resizeTextMinSize=28;label.resizeTextMaxSize=44;
            label.alignment = TextAnchor.MiddleRight; label.color = GoldUiTheme.Cream;
            timeLabel.SetKey("ingame.result_seconds", float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0f : Mathf.Max(0,seconds));
            timeLabel.gameObject.SetActive(true);
            continueButton.transform.SetParent(results,false);
            GoldUiTheme.Place((RectTransform)continueButton.transform,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(0,-152),new Vector2(440,72));
            UiKit.ButtonStyle(continueButton,true);
            UiKit.Key(continueButton.transform,"pause.exit_map");
            // This is the sole result action. Do not navigate back to the hidden legacy window.
            continueButton.navigation = new Navigation { mode = Navigation.Mode.None };
            continueButton.gameObject.SetActive(true);
            Sample(duration);
            SampleResult(0);
        }

        private void Update()
        {
            if (playing)
            {
                elapsed = Mathf.Min(duration, elapsed + Time.unscaledDeltaTime);
                if (elapsed >= duration) playing = false;
            }
            Sample(elapsed);
            if (showingResult)
            {
                resultElapsed += Time.unscaledDeltaTime;
                SampleResult(resultElapsed);
                if (mastered) SampleMaster(resultElapsed);
                if (resultElapsed >= ResultTransitionDuration && resultReady != null)
                {
                    var callback = resultReady; resultReady = null; callback();
                }
            }
        }

        /// <summary>Deterministic timeline sampling also used by the editor's visual verification.</summary>
        public void Sample(float seconds)
        {
            Build();
            float t = Mathf.Clamp01(seconds / duration);
            float fadeIn = Mathf.SmoothStep(0f, 1f, t / 0.09f);
            group.alpha = fadeIn;
            float fit = LayoutFit();
            composition.localScale = Vector3.one * IntroScale(fit);
            composition.anchoredPosition = new Vector2(ColumnX(), 0);
            float reveal = Mathf.Clamp01((t - 0.12f) / 0.18f);
            float settle = 1f - Mathf.Pow(1f - reveal, 3f);
            titleGroup.alpha = Mathf.SmoothStep(0f, 1f, reveal);
            titleRect.localScale = Vector3.one * (1f + 0.23f * (1f - settle));
            titleRect.anchoredPosition = new Vector2(0f, 14f * (1f - settle));
            ornament.Progress = t;
        }

        public void SampleResult(float seconds)
        {
            if (!showingResult) return;
            float progress = Mathf.Clamp01(seconds / ResultTransitionDuration);
            float slide = 1f - Mathf.Pow(1f-progress,3);
            float fit = LayoutFit();
            float x = ColumnX();
            composition.anchoredPosition = new Vector2(x, Mathf.Lerp(0,230f*fit,slide));
            composition.localScale = Vector3.one * Mathf.Lerp(IntroScale(fit),fit*TitleScale*.62f,slide);
            results.localScale = Vector3.one * fit;
            results.anchoredPosition = new Vector2(x, Mathf.Lerp(-170f*fit,-118f*fit,slide));
            resultBackdrop.anchoredPosition=new Vector2(x,0);
            resultBackdrop.localScale=Vector3.one*fit;
            backdropGroup.alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress/.5f));
            resultsGroup.alpha = Mathf.SmoothStep(0,1,Mathf.Clamp01((progress-.25f)/.65f));
            resultsGroup.interactable = progress >= 1;
            resultsGroup.blocksRaycasts = progress >= 1;
        }

        private float ColumnX() => -host.rect.width * 0.5f + host.rect.width * ColumnCentre;

        private float IntroScale(float fit) => Mathf.Max(.01f,Mathf.Min(fit*TitleScale,(host.rect.width*.48f-32f)/1100f));
        private float LayoutFit() => Mathf.Max(.01f,Mathf.Min((host.rect.width*.48f-32f)/620f,(host.rect.height-100f)/800f));

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
