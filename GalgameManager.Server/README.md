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