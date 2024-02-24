<p align="center">
<img src="GalgameManager/Assets/Pictures/Potato.png" width="80px"/>
</p>

<div align="center">
  
# PotatoVN
![123](https://img.shields.io/endpoint?color=blue&label=Microsoft%20Store%20Rating&url=https%3A%2F%2Fmicrosoft-store-badge.fly.dev%2Fapi%2Frating%3FstoreId%3D9P9CBKD5HR3W)
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FGoldenPotato137%2FPotatoVN.svg?type=shield)](https://app.fossa.com/projects/git%2Bgithub.com%2FGoldenPotato137%2FPotatoVN?ref=badge_shield)
[![Telegram](https://img.shields.io/badge/Telegram%E5%90%B9%E6%B0%B4%E7%BE%A4-Join-green)](https://t.me/+gymkuMygUpY1NzY1)

一个VisualNovel管理工具，旨在为galgame屯屯鼠们提供一个方便的游戏管理平台。
</div>

## 功能
* 自动检索文件夹内的游戏
* 自动从多个数据库中获取游戏信息 （目前支持[bangumi](https://bgm.tv/)、[visual novel database](https://vndb.org/)），并从账户中同步游玩状态
* 将游戏存档与云端同步 (此功能需要电脑上具有任意一款同步软件来同步存档文件夹，如OneDrive、NextCloud、坚果云、百度云同步盘等)
* 统计游戏游玩时间
* 从压缩包中自动解压游戏，并自动识别且添加到游戏库中

### 预览
[见应用商店](https://www.microsoft.com/store/apps/9P9CBKD5HR3W)

## 安装
本程序打包格式为MSIX，可以在[微软应用商店](https://www.microsoft.com/store/apps/9P9CBKD5HR3W)下载

**对于windows10用户:** windows10用户需要额外安装[Segoe Fluent 图标字体](https://aka.ms/SegoeFluentIcons)

## 翻译
![en translation](https://img.shields.io/badge/dynamic/json?color=blue&label=en&style=flat&logo=crowdin&query=%24.progress[?(@.data.languageId==%27en%27)].data.translationProgress&url=https%3A%2F%2Fbadges.awesome-crowdin.com%2Fstats-15790227-581621.json)

PotatoVN使用crowdin来进行本地化，欢迎在[crowdin](https://crowdin.com/project/potatovn)将PotatoVN带到您的语言当中。

## 开发者相关
本程序是一个WinUI3的应用，要编译本程序，请参考[微软文档](https://learn.microsoft.com/zh-cn/windows/apps/windows-app-sdk/set-up-your-development-environment?tabs=cs-vs-community%2Ccpp-vs-community%2Cvs-2022-17-1-a%2Cvs-2022-17-1-b)
安装相应的开发环境。

本程序使用MVVM架构，基于[TemplateStudio](https://github.com/microsoft/TemplateStudio/tree/main/docs/WinUI)生成的框架开发。

欢迎各位感兴趣的dalao在[这里](https://github.com/GoldenPotato137/PotatoVN/discussions/categories/%E5%BC%80%E5%8F%91%E7%8A%B6%E6%80%81)查看目前急需解决的问题，PotatoVN永远欢迎各位的加入~

## License
[![FOSSA Status](https://app.fossa.com/api/projects/git%2Bgithub.com%2FGoldenPotato137%2FPotatoVN.svg?type=large)](https://app.fossa.com/projects/git%2Bgithub.com%2FGoldenPotato137%2FPotatoVN?ref=badge_large)
