# 夜のハイウェイ

Mac・Kinectなしで試す場合は `Assets/Scenes/Sandbox/HighwayKeyboard.unity` を使います。[キーボード試遊・分岐・衝突の説明](HighwayKeyboard.md)を参照してください。

`Assets/Scenes/Main.unity` を開いて Play。統合版はKinectを起動し、中央登録後に夜の高速道路を自動前進します。[Kinectで走る手順](PlayKinectHighway.md)を参照してください。Kでキーボードプレビューへ切り替えると、以下の操作を使えます。

- A / D、または左右矢印: 道路内を左右移動。離すと横移動が止まります。
- W / S、または上下矢印: 速度を調整（標準0〜220 km/h）。
- Space: 一時停止 / 再開。
- R: 開始位置・姿勢に戻す。
- H: 操作用HUDを表示 / 非表示。

`NightHighway` の Inspector で速度、視野角、視点の高さ、横移動速度、カメラの傾きを調整できます。標準速度は138 km/hです。**Ride > Maximum Speed Kmh** は初期220で、10,000 km/hまで変更できます。周囲の車は各車ごとに80〜120 km/hのランダムな固定速度で走ります。操作用HUDはOBS向けのカメラ映像に含みません。

KinectHighwayInputが `UseExternalSteering`、`SteeringInput`、`ExternalRideReady`、`ExternalSteeringAvailable` を設定します。道路に沿う前進と横移動は独立し、入力が失われたフレームで横移動を止めます。

道路、車、街灯、都市の窓、星は2つの再利用メッシュにまとめて描画しています。数千個のUnity LightやGameObjectを毎フレーム生成する構成ではありません。車との接触は進行距離・横位置による連続判定で処理し、視点・光・音の演出につなげています。物理エンジンによる車の押し合いや影は含めていません。

ゲームを停止している間も、シーンを開くとプレビューが生成されます。`NightHighway` のコンポーネントメニューの `Refresh preview` から更新できます。`GeneratedRoadGeometry` は一時的な描画データで、シーンには保存しません。

見た目の着想: [Night Freeway](https://mhatta.github.io/nightfreeway/)。道路生成・シェーダー・描画コードはこのUnityデモ用に実装しました。
