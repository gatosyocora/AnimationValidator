# AnimationValidator4VRC

## Overview
Hierarchy上でのオブジェクトの階層を変えたことによって起きるAnimationのmissingを修正します
fork元のものをVRChat用および使いやすく多少の変更をしています

fork元からの変更点は以下の通りです。
* AnimatorOverrideControllerに対応
* 同じ名前のオブジェクトが複数見つかった場合そのリストを表示し、変更後のパスを選択して自動変更するように
* 結果の文字色を一部変更
* VRCSDK2がインポートされている場合、VRC_AvatarDescriptorから対象のAnimationOverrideControllerを取得されるように

## Demo
![result](https://github.com/kyorosuke/AnimationValidator/blob/feature/media/demo_1.gif)
<br>↑fork元の動作DEMOです。基本操作は変わりません。

## Requirements
Unity 2018.4.20f1で動作テストをしました

## How To Use
1. unitypackageをダウンロードして対象のUnityプロジェクトにインポートする
2. 修正したいAnimatorController(またはAnimatorOverrideController)を設定したAnimatorを持ったオブジェクトをHierarchy上で右クリックで選択する
3. 「アニメーションクリップ修正」を選択する
4. 「全部まとめて修正」またはそれぞれの「自動修正」ボタンを選択する<br>自動修正できる場合は緑のチェックが表示される<br>対象オブジェクトと同名のものが複数あるなどの場合は手動作業が必要
  オレンジ色で「同じ名前のオブジェクトが子階層上に複数あります」と表示された場合、下のリストから変更後として適したパスの「Select」ボタンを選択する<br>このパスのリストは元のパスと後ろから比較して一致率が高いパスから順に並んでいる。

## Installation


## Contribution
https://github.com/sukedon/AnimationValidator をforkして作成しました

Fork it ( https://github.com/kyorosuke/AnimationValidator/fork )  
Create your feature branch (git checkout -b my-new-feature)  
Commit your changes (git commit -am 'Add some feature')  
Push to the branch (git push origin my-new-feature)  
Create new Pull Request  


## LICENSE

[MIT Licene](https://github.com/gatosyocora/AnimationValidator/blob/master/LICENSE)
