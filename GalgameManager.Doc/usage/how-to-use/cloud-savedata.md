---
order: 5
---

# 云存档

## 云存档原理
云存档本质上是把游戏原来的存档文件夹**搬到了另外的地方**，并在原来放存档的地方建立了一个类似与快捷链接的东西指向新的存档位置，这样子游戏在访问存档的时候会自动访问新位置下的存档。如果新的放存档的地方是一个具有同步功能的文件夹，那么存档的变化就自动同步到云端上了。

如果你不知道什么是具有同步功能的文件夹，请参考[坚果云的介绍视频](https://www.bilibili.com/video/BV1oK4y157LX/)

下图为详细的图解介绍：
### STEP1
一开始，游戏的存档是一个位于游戏根目录下的文件夹（本软件一样支持非游戏目录下的存档，同步原理是一样的，为了方便介绍，在这里使用游戏根目录下的存档）
![alt text](/usage/cloud-savedata1.png)

图1：存档文件夹`savedata_chs`位于游戏目录下

### STEP2
在软件内转换存档位置后，软件会把存档文件夹搬到设置中指定的文件夹下：
![alt text](/usage/cloud-savedata2.png)

图2：文件夹`savedata_chs`被搬到了`OneDrive下的GameSaves/对应游戏名/savedata_chs` 

### STEP3
完成存档文件夹迁移之后，PotatoVN会在原本存档的位置建立一个指向新位置的“快捷方式”
![alt text](/usage/cloud-savedata3.png)
图3：`savedata_chs`变成了一个“快捷方式”

![alt text](/usage/cloud-savedata4.png)

图4：`savedata_chs`确实指向了onedrive里的文件夹

## 如何操作
### STEP1：选择一个同步盘并创建同步文件夹
目前国内比较主流的同步云盘有：OneDrive、百度云同步盘、坚果云、NextCloud（需要自己搭建服务器）

本教程以百度云同步盘为例，其他的云盘操作类似。

下载相应的软件并在本地设置一个文件夹与云端同步：
![alt text](/usage/cloud-savedata5.png)

![alt text](/usage/cloud-savedata6.png)

### STEP2：新建游戏存档文件夹（选做）
可以在刚刚创建的同步文件夹内新建一个专门用来放游戏存档的文件夹，这样子有利于保持目录整齐。当然，这一步是选做的。
![alt text](/usage/cloud-savedata7.png)

### STEP3：在设置内设置存档根目录
![alt text](/usage/cloud-savedata8.png)

如果你刚刚有新建一个专门用来放游戏存档的文件夹，那么就选择它；否则选择同步文件夹本身。
![alt text](/usage/cloud-savedata9.png)

![alt text](/usage/cloud-savedata10.png)

### STEP4：转换存档位置
![alt text](/usage/cloud-savedata11.png)

**注意：选择存档文件夹，不要选错**
![alt text](/usage/cloud-savedata12.png)

done：
![alt text](/usage/cloud-savedata13.png)

你会发现在游戏里面保存存档，百度云同步盘会会有对应的同步记录
![alt text](/usage/cloud-savedata14.png)


### STEP5：在别的电脑链接到云存档（选做）
如果我有多台设备，要推同一个galgame，我应该怎么同步我的游戏进度呢？这个时候PVN的同步游戏功能就发挥作用了。

你需要在另外一台设备上重复STEP1-STEP3动作。在执行STEP4的时候，你会发现这样的提示（注意：这里需要两台设备上同一个galgame的名字**完全一致**，多一个空格都不行！）：
![alt text](/usage/cloud-savedata15.png)

done！游戏进度自动同步啦，撒花 ✿✿ヽ(°▽°)ノ✿