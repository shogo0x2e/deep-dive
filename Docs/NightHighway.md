# 夜のハイウェイ

`Assets/Scenes/Main.unity` を開いて Play。夜の高速道路を自動前進します。

- A / D、または左右矢印: 道路内を左右移動。離すと横移動が止まります。
- W / S、または上下矢印: 速度を調整（20〜220 km/h）。
- Space: 一時停止 / 再開。
- R: 開始位置・姿勢に戻す。
- H: 操作用HUDを表示 / 非表示。

`NightHighway` の Inspector で速度、視野角、視点の高さ、横移動速度、カメラの傾きを調整できます。標準速度は138 km/hです。操作用HUDはOBS向けのカメラ映像に含みません。

将来Kinectをつなぐ際は `UseExternalSteering` を有効にし、`SteeringInput` に -1（左）〜+1（右）を与えます。道路に沿う前進と横移動は独立しています。入力が失われたら、接続側で0へ戻してください。

道路、車、街灯、都市の窓、星は2つの再利用メッシュにまとめて描画しています。数千個のUnity LightやGameObjectを毎フレーム生成する構成ではありません。影や物理衝突は現在の鑑賞デモには含めていません。

ゲームを停止している間も、シーンを開くとプレビューが生成されます。`NightHighway` のコンポーネントメニューの `Refresh preview` から更新できます。`GeneratedRoadGeometry` は一時的な描画データで、シーンには保存しません。

見た目の着想: [Night Freeway](https://mhatta.github.io/nightfreeway/)。道路生成・シェーダー・描画コードはこのUnityデモ用に実装しました。
