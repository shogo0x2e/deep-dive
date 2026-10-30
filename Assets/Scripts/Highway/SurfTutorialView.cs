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
        public const string ControlsHint = "左にかたむくと左へ    右にかたむくと右へ\n画面側の手を上げるとブレーキ、下げるとアクセル";
        public const string FontCharacters = "HIGHWAY SURFING 0123456789手を前と後ろにひろげてくださいそのまま、少しだけキープただいま準備しています画面の前に立って、全身を見せてくださいからだをまっすぐにして、そのまま静止します肩の高さで腕をのばして、サーフィンのポーズ！ポーズを合わせると、自動で始まります左にかたむくと左へ右にかたむくと右へ夜のハイウェイへようこそ" + ControlsHint;
        void OnEnable()
        {
            if (Input == null || Input.World == null) return;
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
            Group.alpha=Mathf.MoveTowards(Group.alpha,visible?1:0,Time.unscaledDeltaTime*3);
            // Camera-space UI is included in Spout. Keep bloom/tonemapping off the
            // instructions, then restore the night-world treatment for the ride.
            if (cameraData != null) cameraData.renderPostProcessing = restorePostProcessing && Group.alpha <= .001f;
            Group.blocksRaycasts=Group.interactable=false;
            Title.text=Input.TutorialTitle;
            Instruction.text=Input.TutorialMessage;
            ProgressFill.fillAmount=Input.RegistrationProgress;
            ProgressText.text=Input.Rig.Calibrating?"そのまま、少しだけキープ":"ポーズを合わせると、自動で始まります";
        }
    }
}
