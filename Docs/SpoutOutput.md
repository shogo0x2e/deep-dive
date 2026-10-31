# Main Camera → OBS → projector

`Assets/Scenes/Main.unity` を Play すると、Main Camera の映像を **HighwaySurfing.Main** として Spout に送信します。
既定は **2160 × 2160 / 1:1 / 不透明映像**。Unity の Game View の大きさを変更しても送信解像度は変わりません。
Game View には同じ映像を縦横比を保って表示します。参加者向けの開始案内は送信映像に含み、運営者用の操作 HUD は含めません。

## OBS

1. Unity の Main を Play。
2. OBS のソース追加から **Spout2 Capture** を選択。
3. Sender / Spout Sender に **HighwaySurfing.Main** を指定。
4. OBS のキャンバスに映像を合わせ、プレビューの全画面プロジェクターで投影先ディスプレイを選択。

この PC には `C:/ProgramData/obs-studio/plugins/win-spout` が存在することを確認しました。
OBS 上のプラグイン読み込み・実際のプロジェクター投影はユーザー側で確認してください。
ソースが現れない場合は、使用中の OBS に対応した [OBS Spout2 Plugin](https://github.com/Off-World-Live/obs-spout2-plugin) を確認します。

従来の 1920×1080 出力では、Unity 内の一時的な SpoutReceiver で、ネイティブ送信元の列挙、受信画像、
時間経過による画像の更新、運営者用HUDが含まれないことを確認済みです。自動開始チュートリアルの日本語と姿勢図を含む受信画像も確認しました。
出力コンポーネントの停止時には送信元が消え、カメラの出力先が元に戻り、再開後も受信できました。
2160×2160 への変更後も、SpoutReceiver で同サイズの受信、カメラのアスペクト比 1:1、フレーム進行、開始案内が画面内に収まることを確認しました。
OBS での受信・実際の投影は未検証です。

## Unity の設定

Main Camera の **Main Camera Spout Output** コンポーネントで次を調整できます。変更は Play を止めてから行います。

| 設定 | 初期値 | 用途 |
| --- | --- | --- |
| Sender Name | HighwaySurfing.Main | OBS に表示される送信名 |
| Width / Height | 2160 / 2160 | 送信解像度（1:1） |
| Preview In Game View | 有効 | Editor / アプリ内に送信映像を表示 |

同じ GameObject の **Spout Sender** は Edit Mode では無効が正常です。
Play 開始時に出力コンポーネントが RenderTexture と送信元を設定し、有効化します。
Stop / コンポーネント無効化時は送信を止め、カメラの元の出力先に戻します。
組み直す場合は `Tools > Highway Surfing > Configure Main Camera Spout Output` を使い、シーンを保存します。

## 参加者向けの速度表示

**PlayerSpeedHud** は映像の左右中央・上から30%の位置に現在の速度を **138 km/h** の形式で表示します。
`NightHighwayWorld.SpeedKmh` を整数に丸めて表示し、加速・ブレーキ・衝突による減速に追従します。運営者用 HUD の表示設定とは独立しています。
Spout と Game View の両方に含まれます。開始案内モーダルの背面に描画し、**ProjectionDebugView** がオンの間は単色表示に隠れます。

## 投影合わせ用のデバッグ表示

Hierarchy のルートにある **ProjectionDebugView** を選び、Inspector 最上部の GameObject のチェックを切り替えます。初期状態はオフです。

- **オン**：2160×2160 の送信映像全体を緑色にします。
- **オフ**：通常のカメラ映像に戻ります。
- **Projection Debug View > Color**：緑以外の単色にも変更できます。

Play 中にも切り替え可能で、Spout と Game View 内の映像プレビューに同じ色が出ます。開始案内やポストプロセスの上から不透明な単色で覆います。ゲームの進行・音声は継続します。
コンポーネント自体のチェックを外してもデバッグ表示を解除できます。

## 技術構成

- [KlakSpout](https://github.com/keijiro/KlakSpout) **2.0.6** を公式 Git リポジトリのコミット `849e7bca3c167839ed697796153e1749acf0c53f` に固定。
- Main Camera → sRGB ARGB32 RenderTexture → KlakSpout Texture sender。走行中はURPのポストプロセスを含みます。開始案内中は文字の可読性のため一時停止します。
- 参加者向けのPlayerTutorialはScreen Space Cameraで描画し、出力映像に含みます。
- Game View のプレビューと操作 HUD は Screen Space Overlay。出力カメラには入りません。
- Windows / Direct3D 11 または 12 が必要です。このプロジェクトの Editor は Direct3D 12。
- 黒い映像になる場合は Unity と OBS が同じ GPU を使用しているか確認します。特に内蔵 GPU + NVIDIA GPU のノート PC で重要です。
- Main Camera の子に World Space / Screen Space Camera の UI を追加した場合、その UI はカメラ映像に含まれます。
- 他プロセスの同名 Sender と区別できるよう、複数インスタンスを実行する場合は Sender Name を変更します。

録画・配信・OBS のシーン設定の変更は、この Unity プロジェクトからは行いません。
