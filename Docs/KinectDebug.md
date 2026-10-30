# Kinect 配置比較 Sandbox

シーン: Assets/Scenes/Sandbox/KinectDebug.unity  
Unity 6000.3.26f1 / URP 17.3.0 / Windows x64 / Azure Kinect DK

Mainのワールドとは独立した比較シーンです。PlayだけではKinectを起動しません。RGB・音声・録画は取得しません。

## 最初の操作

1. Kinectの電源とUSB 3を接続し、他のKinect Viewerを閉じます。
2. シーンを開いてPlay → **START KINECT**。
3. 1人で全身が映る位置に立ち、胸・腰のconfidenceが **M** であることを確認します。
4. サーフィン姿勢（腕を画面の奥行き方向へ広げる）を取り、**CALIBRATE / C**を押して1.5秒静止します。
5. 画面の左へ傾いてマイナス、右へ傾いてプラスになるかを確認します。板と緑のメーターが操舵量です。

橙はデッドゾーン適用後・平滑化前、緑は平滑化後の入力です。TORSO ANGLEは画面座標の鉛直からの絶対角度、LEANは登録した中立姿勢からの角度です。PELVIS SHIFTは中立位置からの腰の横移動で、操舵には混ぜず比較用に表示します。

## 3配置の比較

上から見て画面側を0°とします。数字キー1 / 2 / 3 またはボタンで切り替えます。

| プリセット | 仮想Kinectの場所 | 実機で試すこと |
|---|---|---|
| SCREEN | 画面側、人物へ向く | 横乗り姿勢が側面から見える場合の取得率 |
| 45 DEG（初期値） | 画面と横の中間 | 胸と腰が見え、左右動作も分離できるか |
| SIDE | 画面に対して90°横 | 胸が見える側からの奥行き方向の検出 |

**プリセットは仮想SensorOriginを動かすだけです。実際のKinectは手で移動してください。** 距離2.5 m、高さ1.1 mは開始値で、最適配置を保証する値ではありません。

**SWAP SENSOR SIDE**で45°/90°を反対側に切り替えられます。前に出す足を変えると胸側・背中側も変わるので、regular/goofyの比較に使います。鏡表示による左右反転ではありません。0°では場所は変わりません。

各配置で距離・高さを揃え、次を試します。

- RESET STATS → CALIBRATE。
- 中立で60秒、左右10回ずつ、傾いたまま保持。
- 腕だけ動かす、しゃがむ、画面を見るために首を動かす。
- 一度外れて戻る。複数人が映る場合はRELEASE BODY後に1人だけ入る。

VALID SAMPLESとTRACKING LOSSESは簡易指標です。新しい結果フレーム単位で数え、フレームが全く届かない時間は分母に入りません。精度の割合ではありません。SDKのMは位置が正しいことの保証ではなく、低信頼度の関節は橙色の予測として表示します。

## 親オブジェクトをEditorで調整する

Hierarchyの **SensorOrigin_EDIT_POSITION_AND_ROTATION** が全32関節とボーンの親です。Position / Rotationを操作すると子骨格が追従します。

- SDKの(x,y,z) mm → 子のlocalPosition (x,-y,z) * 0.001 m。
- 子のworld position → **ScreenReference_FixedAxes**のright / upに投影。
- 胸−腰の傾き → 中立補正 → デッドゾーン → 平滑化。

**位置だけでは設置角度は直りません。Rotation（主にY、必要ならX / Z）も実機に合わせてください。** Scaleは(1,1,1)のままにします。画面・床・Kinectの実測位置に合わせると、骨格と床の位置も比較できます。

ScreenReferenceはSensorOriginの子ではありません。画面右は+X、上は+Y、画面へ向かう方向は+Zです。デバッグカメラの向きに操舵軸は連動しません。親またはScreenReferenceを変えると校正を解除し、再登録まで操舵を0にします。Play中のInspector変更は通常Play終了で戻るので、最終値はEdit Modeでも設定して保存してください。

胸と腰の共通の平行移動は傾きには影響しません。全身の横移動はPELVIS SHIFTで確認します。骨格から足裏の荷重そのものは測れません。

## 調整パラメーター

KinectDebugのInspector:

- Dead Zone Degrees: 初期3°。中立の揺れを除く範囲。
- Full Scale Degrees: 初期18°。入力が±1になる角度。
- Smoothing Seconds: 初期0.08秒。
- Maximum Frame Age: 初期0.3秒。超過時・胸/腰の信頼度不足時は即0入力。
- Use 30 Fps: 初期OFF（15 FPS）。STOP→STARTで反映。
- Processing Backend: 初期DirectML。CPUも選択可能。
- Runtime Directory: 初期 .Local/AzureKinect/Runtime。絶対パスも指定可能。

RESULT AGEは**推論結果を受信してからの経過時間**です。撮影から推論までや表示遅延を含みません。総遅延は身体と画面を同時撮影して別途評価してください。

短い追跡落ちではBody IDを維持し、2秒以上見失った後に1人だけ検出された場合は再選択します。新しいBody ID取得後は再校正が必要です。接続失敗・切断後は理由を表示し、STOP→STARTで再試行します。

## 実機なしの表示確認

**SIMULATION / ARROW KEYS**で明示的に合成骨格へ切り替えます。入力断で勝手にSimulationへ移行しません。

- CALIBRATE後、← / →で±15°を試す。
- InspectorのSimulation Lean Degreesでも連続調整可能。
- Simulation SweepをONにすると自動で左右に傾く。
- START KINECTで実入力へ戻す（必要ならSTOPを待つ）。

合成骨格はTransformと操舵処理の確認用であり、実機精度の証拠ではありません。Edit Modeの骨格も位置関係のプレビューです。

## 別PCでのSDKセットアップ

プロジェクトルートのPowerShellで ./Tools/SetupAzureKinect.ps1 を実行します。

Microsoft公式NuGetのSensor 1.4.1 / Body Tracking 1.1.2 / ONNX Runtime 1.10.0をSHA-256検証して取得します。モデル・DLLは.Localに入りGit対象外です。現在の作業PCには既存の互換ランタイムをコピー済みです。

Windows x64、Kinectの電源、USB 3、Visual C++ x64ランタイムと深度カメラのドライバーが必要です。初回Tracker生成・停止には数秒かかる場合があります。

## 検証

KinectGeometryChecks.Run()をUnity内で実行すると、0/45/90度相当のセンサー変換、親の位置・回転、画面軸、左右符号、中立登録、追跡喪失、古いフレーム、NaNを確認します。

実機3配置での取得率・誤操作・操作感はユーザーの確認対象です。StandaloneビルドのDLL配置・配布はこのEditor向けPoCでは未検証です。

### 2026-10-30 の実施結果

- Unity実コンパイル: Error 0 / Warning 0。
- KinectGeometryChecks: 13項目PASS。
- Playでフレームカウンター進行を確認。実機は自動起動されずCaptureActive=false。
- 合成骨格で中立登録後、+15° / -15°が操舵約+0.80 / -0.80。
- 親Position変更で校正解除・操舵0になることをPlayで確認。
- 修正後HUDの画像を目視確認。Console Error 0 / Warning 0。
- 実KinectのStart・3配置での精度評価は未実施。ランタイムはローカル配置済み。
