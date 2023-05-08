# PotatoVN
TODO: 这里应该放介绍图

## 介绍
PotatoVN是一个用于管理galgame的游戏的工具，旨在为galgame屯屯鼠们提供一个方便的游戏管理平台。

### 功能
* 自动检索文件夹内的游戏
* 自动从多个数据库中获取游戏信息
* 将游戏存档与云端同步
* 从压缩包中自动解压游戏

### 预览
[见应用商店](https://www.microsoft.com/store/apps/9P9CBKD5HR3W)

## 安装
本程序打包格式为MSIX，可以在[微软应用商店](https://www.microsoft.com/store/apps/9P9CBKD5HR3W)下载

**对于windows10用户:**

windows10用户需要额外安装[Segoe Fluent 图标字体](https://aka.ms/SegoeFluentIcons)

## 翻译
欢迎在[crowdin](https://crowdin.com/project/potatovn)帮助PotatoVN本地化。

## 开发者相关
本程序是一个WinUI3的应用，要编译本程序，请参考[微软文档](https://learn.microsoft.com/zh-cn/windows/apps/windows-app-sdk/set-up-your-development-environment?tabs=cs-vs-community%2Ccpp-vs-community%2Cvs-2022-17-1-a%2Cvs-2022-17-1-b)
安装相应的开发环境。

本程序使用MVVM架构，基于[TemplateStudio](https://github.com/microsoft/TemplateStudio/tree/main/docs/WinUI)生成的框架开发。

### TODO
always a lot

- [ ] **BUG**: 游戏匹配规则不能正确响应换行键
- [ ] **FEAT**: 记录游戏时长
- [ ] **FEAT** : 删除库
- [ ] **UI** : 黑色主题下标题颜色&三大金刚键颜色
- [ ] **UI** : 设置界面加入滚动条显示
- [ ] **FEAT**: 允许在Library界面直接更改游戏ID
- [ ] **BUG**: 扫描游戏时扫到没有权限的目录会抛异常
- [ ] **FEAT** : 游戏分组功能/打TAG功能
- [ ] **FEAT**: 笔记功能
- [ ] **BUG**: 同时扫描多个库的时候图片显示不正确
- [ ] **FEAT**: 导出游戏元数据的功能
- [x] ~~**BUG**: 编辑游戏界面删掉图片地址后抛异常~~
- [x] ~~**BUG**: 部分情况软链接生成失败~~
- [x] ~~**FEAT**: 限制递归搜索子目录的深度~~ [Thanks to Murlors](https://github.com/GoldenPotato137/GalgameManager/pull/26)
