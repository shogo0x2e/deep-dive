using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HighwaySurfing
{
    public sealed class SurfTutorialView : MonoBehaviour
    {
        public KinectHighwayInput Input;
        public CanvasGroup Group;
        public TextMeshProUGUI Title, Instruction, ProgressText;
        public UnityEngine.UI.Image ProgressFill;
        UniversalAdditionalCameraData cameraData;
        bool restorePostProcessing;
        HighwayAttractMode attract;
        public const string ControlsHint = "左にかたむくと左へ    右にかたむくと右へ\n画面側の手を上げるとブレーキ、下げるとアクセル";
        public const string FontCharacters = "HIGHWAY SURFING 0123456789手を前と後ろにひろげてくださいそのまま、少しだけキープただいま準備しています画面の前に立って、全身を見せてくださいからだをまっすぐにして、そのまま静止します肩の高さで腕をのばして、サーフィンのポーズ！ポーズを合わせると、自動で始まります左にかたむくと左へ右にかたむくと右へ夜のハイウェイへようこそ" + ControlsHint + SurfPoseGuidanceText.FontCharacters;
        void OnEnable()
        {
            if (Input == null || Input.World == null) return;
            attract = Input.GetComponent<HighwayAttractMode>();
            cameraData = Input.World.MainCamera.GetUniversalAdditionalCameraData();
            restorePostProcessing = cameraData.renderPostProcessing;
        }
        void OnDisable()
        {
            if (cameraData != null) cameraData.renderPostProcessing = restorePostProcessing;
        }
        void LateUpdate()
        {
            if(Input==null)return;
            bool visible=Input.ShowTutorial;
            bool demo = attract != null && attract.isActiveAndEnabled && attract.DemoActive;
            Group.alpha=Mathf.MoveTowards(Group.alpha,visible ? (demo ? attract.ModalOpacity : 1) : 0,Time.unscaledDeltaTime*3);
            // Camera-space UI is included in Spout. Keep bloom/tonemapping off the
            // instructions, then restore the night-world treatment for the ride.
            if (cameraData != null) cameraData.renderPostProcessing = restorePostProcessing && (demo || Group.alpha <= .001f);
            Group.blocksRaycasts=Group.interactable=false;
            Title.text=demo ? "夜のハイウェイへ\nようこそ" : Input.TutorialTitle;
            Instruction.text=demo ? "手を前と後ろにひろげて、\nサーフィンのポーズ！" : Input.TutorialMessage;
            ProgressFill.fillAmount=Input.RegistrationProgress;
            ProgressText.text=demo ? "画面の前に立って、全身を見せてください" :
                Input.Rig.Calibrating && Input.RelaxedEntry ? "そのまま1秒、維持してください" : "ポーズを合わせると、自動で始まります";
        }
    }
}
