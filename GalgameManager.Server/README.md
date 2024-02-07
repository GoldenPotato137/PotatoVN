## 在测试环境中运行
初始化用户密钥:
```shell
dotnet user-secrets init
```
设置各类环境变量：
```shell
dotnet user-secrets set "Key" "Value"
```
应该设置的环境变量有（Key）：
* `ConnectionStrings:DefaultConnection` 数据库连接字符串，**数据库必须是MySQL**
* `AppSettings:JwtKey` JWT秘钥，**至少64位长**
* `AppSettings:Minio:EndPoint` Minio服务器地址与端口, 如: 114514.moe:1919
* `AppSettings:Minio:AccessKey` Minio用户识别码
* `AppSettings:Minio:SecretKey` Minio用户密钥

可以选填的环境变量有：
* `AppSettings:Minio:BucketName` Minio存储桶名称，默认为`potatovn`，如果你创建的桶的名字不是`potatovn`，你需要设置这个环境变量为你的桶的名字
* `AppSettings:Minio:UseSSL` 是否使用SSL，默认为`false`，如果你的Minio服务器配置了SSL，可以设置这个环境变量为`true`
* `AppSettings:Bangumi:OAuth2Enable` 是否作为Bangumi OAuth2认证服务器，默认为`false`，
如果填写为`true`则必须设置AppId和AppSecret
* `AppSettings:Bangumi:AppId` Bangumi第三方应用的AppId
* `AppSettings:Bangumi:AppSecret` Bangumi第三方应用的AppSecret
* `AppSettings:User:Default` 是否允许用户以用户名密码注册与登录，默认为`true`
* `AppSettings:User:Bangumi` 是否允许用户使用Bangumi账号注册与登录，默认为`false`