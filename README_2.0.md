
# Rougamo - 肉夹馍

中文 | [English](README_en.md)

## Rougamo是什么
Rougamo是一个静态代码织入的AOP组件，同为APO组件较为常用的有Castle、Autofac、AspectCore等，与这些组件不同的是，这些组件基本都是通过动态代理+IoC的方式实现AOP，是运行时完成的，而Rougamo是编译时直接修改目标方法织入IL代码的。如果你还知道一个AOP组件"PostSharp"，那么Rougamo就是类似Postsharp的一个组件，Postsharp是一个成熟稳定的静态代码织入组件，但PostSharp是一款商业软件，一些常用的功能在免费版本中并不提供。

# 织入方式

## 快速开始
```csharp
// 1.NuGet引用Rougamo.Fody
// 2.定义类继承MoAttribute，同时定义需要织入的代码
public class LoggingAttribute : MoAttribute
{
    public override void OnEntry(MethodContext context)
    {
        // 从context对象中能取到包括入参、类实例、方法描述等信息
        Log.Info("方法执行前");
    }

    public override void OnException(MethodContext context)
    {
        Log.Error("方法执行异常", context.Exception);
    }

    public override void OnSuccess(MethodContext context)
    {
        Log.Info("方法执行成功后");
    }

    public override void OnExit(MethodContext context)
    {
        Log.Info("方法退出时，不论方法执行成功还是异常，都会执行");
    }
}

// 3.应用Attribute
public class Service
{
    [Logging]
    public static int Sync(Model model)
    {
        // ...
    }

    [Logging]
    public async Task<Data> Async(int id)
    {
        // ...
    }
}
```

## 特征匹配
在快速开始中介绍了如何将代码织入到指定方法上，但实际使用时，一个项目中可能有很多方法都需要应用该Attribute，如果手动到每个方法上增加这样一个Attribute，那么将会很繁琐且侵入性比较大，所以`MoAttribute`设计为可以应用于方法(method)、类(class)、程序集(assembly)和模块(module)，同时可以通过`MoAttribute`的`Flags`属性选择符合特征的方法，`Flags`目前可以设置以下特征：
- 方法可访问性（public或者非public）
- 是否静态方法
- 方法/属性/getter/setter/构造方法

```csharp
// 在继承MoAttribute的同时，重写Flags属性，未重写时默认InstancePublic(可访问性为public的实例方法或属性)
public class LoggingAttribute : MoAttribute
{
    // 所有的public方法，不论是静态还是实例
    public override AccessFlags Flags => AccessFlags.Public | AccessFlags.Method;

    // 所有的实例方法属性方法（getter和setter），不论是静态还是实例，普通方法不会被选择
    // public override AccessFlags Flags => AccessFlags.Instance | AccessFlags.Property;

    // 方法重写省略
}

// 2.应用
// 2.1.应用于类上
[Logging]
public class Service
{
    // ...
}

// 2.2.应用于程序集上
[assembly: Logging]
```

需要注意的是，在2.0版本之前`Flags`仅支持设置方法可访问性和是否静态方法，并不支持区分方法/属性/构造方法，在2.0版本前如果将`Attribute`应用于`class/module/assembly`，那么默认会匹配方法和属性的getter/setter。在2.0版本以后，为了兼容老的逻辑，在`Flags`不指定`Method/PropertyGetter/PropertySetter/Property/Constructor`中的任意一个时，那么默认就是方法和属性（`Method|Property`），所以如果在批量应用时你希望仅仅应用于方法时，就需要手动为`Flags`指定`AccessFlags.Method`。

## 接口织入
在前面介绍的方式中，我们主要是通过`Attribute`或应用到方法上或应用到类或程序集上，这种方式会需要我们修改项目代码，这是一种侵入式的织入方式，这种方式对于AOP埋点频繁的场景并不友好。此时通过接口织入的方式，可以大大降低甚至是避免侵入式织入。

```csharp
// 1.定义需要织入的代码（接口织入的方式可以直接实现IMo，当然也可以继续继承MoAttribute）
public class LoggingMo : IMo
{
    // 1.1.特征过滤
    public override AccessFlags Flags => AccessFlags.All | AccessFlags.Method;

    public override void OnEntry(MethodContext context)
    {
        // 从context对象中能取到包括入参、类实例、方法描述等信息
        Log.Info("方法执行前");
    }

    public override void OnException(MethodContext context)
    {
        Log.Error("方法执行异常", context.Exception);
    }

    public override void OnExit(MethodContext context)
    {
        Log.Info("方法退出时，不论方法执行成功还是异常，都会执行");
    }

    public override void OnSuccess(MethodContext context)
    {
        Log.Info("方法执行成功后");
    }
}

// 2.应用空接口
public class TheService : ITheService, IRougamo<LoggingMo>
{
    // ...
}
```

在上面的示例中，需要织入代码的类只需要实现`IRougamo<LoggingMo>`这样一个空接口，与`LoggingMo`定义的`Flags`匹配的方法就会被织入`LoggingMo`的代码。看完上面的示例你可能会有个疑问，这不是还是要修改代码，不还是侵入式的吗。是的，上面的示例还是具侵入式的，但如果你们的项目会对一些通用层进行封装，比如上面Service层的`ITheService`你们有统一的基础接口或者`Service`具有统一的父类，那么此时就可以在父类/基础接口上操作了：

```csharp
// 在统一基础接口上直接应用IRougamo空接口
public interface IService : IRougamo<LoggingMo>
{
    // ...
}

// 业务接口实现基础接口
public interface ITheService : IService
{
    // ...
}

public class TheService : ITheService
{
    // ...
}
```

这种场景其实是很常见的，很多公司都会有自己的一套基础框架，在基础框架中对不同的层会定义一套基础父类/接口，比如`Service/Repository/Cache`等，这时候我们便可以直接在基础框架中通过`IRougamo`空接口的方式直接定义AOP，这样对于业务来说便是完全无侵入的了。

`IRougamo`能够带来更好的无侵入式体验，但也有其局限性，静态类无法实现接口，灵活性也不如`Attribute`，所以两者结合使用，才是最佳的使用方式。

## 表达式匹配
前面介绍了一种匹配应用的方式——[特征匹配](#特征匹配)，这种方式简单易上手，对于粒度较大的匹配模式是很合适的，但这种方式的主要缺点就是粒度过大，无法进行更为精确的匹配，即使是后续的更新，也只是支持区分普通方法/属性/构造方法，无法做更为精确的匹配细分。

在快速扫描了几遍作者有限的认知后，最终确定参考java的一个aop组件aspectj，使用字符串表达式进行匹配，这种方式扩展性强，可以完成绝大部分匹配需求。需要强调的是，由于作者对aspectj也不是完全熟练，所以Rougamo的表达式规则和aspectj可能存在一定出入，同时也对C#的语法特性增加了一些语法，推荐有aspectj的朋友也简单了解一下再进行使用。

### 基础概念
特征匹配是重写`Flags`属性，对应的表达式匹配是重写`Pattern`属性，由于表达式匹配和特征匹配都是用于过滤/匹配方法的，所以两个不能同时使用，`Pattern`优先级高于`Flags`，当`Pattern`不为`null`时使用`Pattern`，否则使用`Flags`。

表达式共支持六种匹配规则，表达式必须是六种的其中一种：
- `method([modifier] returnType declaringType.methodName([parameters]))`
- `getter([modifier] returnType declaringType.propertyName)`
- `setter([modifier] returnType declaringType.propertyName)`
- `property([modifier] returnType declaringType.propertyName)`
- `execution([modifier] returnType declaringType.methodName([parameters]))`
- `regex(REGEX)`

从上面列出的六种规则，你可能会发现并没有构造方法，构造方法和表达式都是2.0新增的功能，构造方法的实现还在表达式之后，不过这并不是主要原因，主要原因在于构造方法的特殊性。对构造方法进行AOP操作其实是很容易出现问题的，比较常见的就是在AOP时使用了还未初始化的字段/属性，所以我一般认为，对构造方法进行AOP时一般是指定特定构造方法的，一般不会进行批量匹配织入。所以目前对于构造方法的织入，推荐直接在构造方法上应用`Attribute`进行精确织入。同时现在表达式不实现构造方法也是为以后留下操作空间，目前没有想好构造方法的表达式格式，等大家使用一段时间后，可以综合大家的建议再考虑。

上面列出的六种匹配规则，除了`regex`的格式特殊，其他的五种匹配规则的内容主要包含以下五个（或以下）部分：
- `[modifier]`，访问修饰符，可以省略，省略时表示匹配所有，访问修饰符包括以下七个：
    - `private`
    - `internal`
    - `protected`
    - `public`
    - `privateprotected`，即`private protected`
    - `protectedinternal`，即`protected internal`
    - `static`，需要注意的是，省略该访问修饰符表示既匹配静态也匹配实例，如果希望仅匹配实例，可以与逻辑修饰符`!`一起使用：`!static`
- `returnType`，方法返回值类型或属性类型，类型的格式较为复杂，详见[类型匹配格式](#类型匹配格式)
- `declaringType`，声明该方法/属性的类的类型，[类型匹配格式](#类型匹配格式)
- `methodName/propertyName`，方法/属性的名称，名称可以使用`*`进行模糊匹配，比如`*Async`,`Get*`,`Get*V2`等，`*`匹配0或多个字符
- `[parameters]`，方法参数列表，Rougamo的参数列表匹配相对简单，没有aspectj那么复杂，仅支持任意匹配和全匹配
    - 使用`..`表示匹配任意参数，这里说的任意是指任意多个任意类型的参数
    - 如果不进行任意匹配，那么就需要指定参数的个数及类型，当然类型是按照[类型匹配格式](#类型匹配格式)进行匹配的，并不是说只能匹配指定类型。这里主要时候不能像aspectj一样进行参数个数模糊匹配，比如`int,..,double`是不支持的

### 类型匹配格式
#### 类型格式
首先我们明确，我们表达某一个类型时有这样几种方式：类型名称；命名空间+类型名称；程序集+命名空间+类型名称。由于Rougamo的应用上限是程序集，同时为了严谨，Rogamo选择使用命名空间+类型名称来表达一个类型。命名空间和类型名称之间的连接采用我们常见的点连接方式，即`命名空间.类型名称`。

#### 嵌套类
嵌套类虽然使用不多，但该支持的还是要支持到。Rougamo使用`/`作为嵌套类连接符，这里与平时编程习惯里的连接符`+`不一致，主要是考虑到`+`是一个特殊字符，表示[子类](#子类匹配)，为了方便阅读，所以采用了另一个字符。比如`a.b.c.D/E`就表示命名空间为`a.b.c`，外层类为`D`的嵌套类`E`。当然嵌套类支持多层嵌套。

#### 泛型
需要首先声明的是，泛型和`static`一样，在不声明时匹配全部，也就是不声明泛型定义时既匹配非泛型类型也匹配泛型类型，如果希望仅匹配非泛型类型或仅匹配泛型类型时需要额外定义，泛型的相关定义使用`<>`表示。
- 仅匹配非泛型类型：`a.b.C<!>`，使用逻辑非`!`表示不匹配任何泛型
- 匹配任意泛型：`a.b.C<..>`，使用两个点`..`表示匹配任意多个任意类型的泛型
- 匹配指定数量任意类型泛型：`a.b.C<,,>`，示例表示匹配三个任意类型泛型，每添加一个`,`表示额外匹配一个任意类型的泛型，你可能已经想到了`a.b.C<>`表示匹配一个任意类型的泛型

#### 模糊匹配

#### 子类匹配

### 表达式——method
- 匹配目标：普通方法（不包含属性getter/setter，不包含构造方法）
- 表达式格式：`method([modifier] returnType declaringType.methodName([parameters]))`
- 全匹配示例：`method(* *(..))`

表达式主要包含两大部分，第一部分`method()`为固定规则申明部分，括号内部为匹配规则。匹配规则包含五个部分：
- `[modifier]`：访问修饰符，

### 表达式——getter

### 表达式——setter

### 表达式——property

### 表达式——execution

### 表达式——regex

# 编织功能

## 异常处理和修改返回值（v1.1.0）
在`OnException`方法中可以通过调用`MethodContext`的`HandledException`方法表明异常已处理并设置返回值，
在`OnEntry`和`OnSuccess`方法中可以通过调用`MethodContext`的`ReplaceReturnValue`方法修改方法实际的返回值，需要注意的是，
不要直接通过`ReturnValue`、`ExceptionHandled`等这些属性来修改返回值和处理异常，`HandledException`和
`ReplaceReturnValue`包含一些其他逻辑，后续可能还会更新。同时还需要注意，`Iterator/AsyncIterator`没有该功能。
```csharp
public class TestAttribute : MoAttribute
{
    public override void OnException(MethodContext context)
    {
        // 处理异常并将返回值设置为newReturnValue，如果方法无返回值(void)，直接传入null即可
        context.HandledException(this, newReturnValue);
    }

    public override void OnSuccess(MethodContext context)
    {
        // 修改方法返回值
        context.ReplaceReturnValue(this, newReturnValue);
    }
}
```

## 重写方法参数（v1.3.0）
在`OnEntry`中可以通过修改`MethodContext.Arguments`中的元素来修改方法的参数值，为了兼容在没有该功能的老版本中可能使用`MethodContext.Arguments`
存储一些临时值的情况（虽然可能性很小），所以还需要将`MethodContext.RewriteArguments`设置为`true`来确认重写参数。
```csharp
public class DefaultValueAttribute : MoAttribute
{
    public override void OnEntry(MethodContext context)
    {
        context.RewriteArguments = true;

        // 判断参数类型最好通过下面ParameterInfo来判断，而不要通过context.Arguments[i].GetType()
        // 因为context.Arguments[i]可能为null
        var parameters = context.Method.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(string) && context.Arguments[i] == null)
            {
                context.Arguments[i] = string.Empty;
            }
        }
    }
}

public class Test
{
    // 当传入null值时将返回空字符串
    [DefaultValue]
    public string EmptyIfNull(string value) => value;
}
```

## 对async/non-async方法统一处理逻辑（v1.2.0）
在1.2.0版本之前，对类似方法签名为`Task<int> M()`的方法，在使用async语法时，通过`MethodContext.ReturnValue`获取到的返回值的类型为`int`，
而在没有使用async语法时获取到的返回值的类型为`Task<int>`这个并不是bug，而是最初的设定，正如我们在使用async语法时代码return的值为`int`，而
在没有使用async语法时代码return的值为`Task<int>`那样。但在刚接触该项目时，可能很难注意到这个设定并且在使用过程中也希望没有使用async语法的方
法能在Task执行完之后再执行`OnSuccess/OnExit`，所以1.2.0推出了`ExMoAttribute`，对使用和不使用async语法的`Task/ValueTask`返回值方法采用
此前使用async语法方法的处理逻辑。

**需要注意`ExMoAttribute`和`MoAttribute`有以下区别：**
- `ExMoAttribute`可重写的方法名为`ExOnEntry/ExOnException/ExOnSuccess/ExOnExit`
- `ExMoAttribute`的返回值通过`MethodContext.ExReturnValue`获取，通过`MethodContext.ReturnValue`获取到的会是`Task/ValueTask`值
- `ExMoAttribute`返回值是否被替换/设置，通过`MethodContext.ExReturnValueReplaced`获取，通过`MethodContext.ReturnValueReplaced`获取到的一般都为true（因为替换为ContinueWith返回的Task了）
- `ExMoAttribute`的返回值类型通过`MethodContext.ExReturnType`获取，`ReturnType/RealReturnType/ExReturnType`三者之间有什么区别可以看各自的属性文档描述或`ExMoAttribute`的类型文档描述

```csharp
[Fact]
public async Task Test()
{
    Assert.Equal(1, Sync());
    Assert.Equal(-1, SyncFailed1());
    Assert.Throws<InvalidOperationException>(() => SyncFailed3());

    Assert.Equal(1, await NonAsync());
    Assert.Equal(-1, await NonAsyncFailed1());
    Assert.Equal(-1, await NonAsyncFailed2());
    await Assert.ThrowsAsync<InvalidOperationException>(() => NonAsyncFailed3());
    await Assert.ThrowsAsync<InvalidOperationException>(() => NonAsyncFailed4());

    Assert.Equal(1, await Async());
    Assert.Equal(-1, await AsyncFailed1());
    Assert.Equal(-1, await AsyncFailed2());
    await Assert.ThrowsAsync<InvalidOperationException>(() => AsyncFailed3());
    await Assert.ThrowsAsync<InvalidOperationException>(() => AsyncFailed4());
}

[FixedInt]
static int Sync()
{
    return int.MaxValue;
}

[FixedInt]
static int SyncFailed1()
{
    throw new NotImplementedException();
}

[FixedInt]
static int SyncFailed3()
{
    throw new InvalidOperationException();
}

[FixedInt]
static Task<int> NonAsync()
{
    return Task.FromResult(int.MinValue);
}

[FixedInt]
static Task<int> NonAsyncFailed1()
{
    throw new NotImplementedException();
}

[FixedInt]
static Task<int> NonAsyncFailed2()
{
    return Task.Run(int () => throw new NotImplementedException());
}

[FixedInt]
static Task<int> NonAsyncFailed3()
{
    throw new InvalidOperationException();
}

[FixedInt]
static Task<int> NonAsyncFailed4()
{
    return Task.Run(int () => throw new InvalidOperationException());
}

[FixedInt]
static async Task<int> Async()
{
    await Task.Yield();
    return int.MaxValue / 2;
}

[FixedInt]
static async Task<int> AsyncFailed1()
{
    throw new NotImplementedException();
}

[FixedInt]
static async Task<int> AsyncFailed2()
{
    await Task.Yield();
    throw new NotImplementedException();
}

[FixedInt]
static async Task<int> AsyncFailed3()
{
    throw new InvalidOperationException();
}

[FixedInt]
static async Task<int> AsyncFailed4()
{
    await Task.Yield();
    throw new InvalidOperationException();
}

class FixedIntAttribute : ExMoAttribute
{
    protected override void ExOnException(MethodContext context)
    {
        if (context.Exception is NotImplementedException)
        {
            context.HandledException(this, -1);
        }
    }

    protected override void ExOnSuccess(MethodContext context)
    {
        context.ReplaceReturnValue(this, 1);
    }
}
```

## 重试（v1.4.0）
从1.4.0版本开始，可以在遇到指定异常或者返回值非预期值的情况下重新执行当前方法，实现方式是在`OnException`和`OnSuccess`中设置`MethodContext.RetryCount`值，在`OnException`和`OnSuccess`执行完毕后如果`MethodContext.RetryCount`值大于0那么就会重新执行当前方法。
```csharp
internal class RetryAttribute : MoAttribute
{
    public override void OnEntry(MethodContext context)
    {
        context.RetryCount = 3;
    }

    public override void OnException(MethodContext context)
    {
        context.RetryCount--;
    }

    public override void OnSuccess(MethodContext context)
    {
        context.RetryCount--;
    }
}

// 应用RetryAttribute后，Test方法将会重试3次
[Retry]
public void Test()
{
    throw new Exception();
}
```
**针对异常处理重试的场景，我独立了 [Rougamo.Retry](https://github.com/inversionhourglass/Rougamo.Retry) 这个项目，如果只是针对某种异常进行重试操作可以直接使用 [Rougamo.Retry](https://github.com/inversionhourglass/Rougamo.Retry)**

使用重试功能需要注意以下几点：
- 在通过`MethodContext.HandledException()`处理异常或通过`MethodContext.ReplaceReturnValue()`修改返回值时会直接将`MethodContext.RetryCount`置为0，因为手动处理异常和修改返回值就表示你已经决定了该方法的最终结果，所以就不再需要重试了
- `MoAttribute`的`OnEntry`和`OnExit`只会执行一次，不会因为重试而多次执行
- 尽量不要在`ExMoAttribute`中使用重试功能，除非你真的知道实际的处理逻辑。思考下面这段代码，`ExMoAttribute`无法在`Task`内部报错后重新执行整个外部方法
  ```csharp
  public Task Test()
  {
    DoSomething();

    return Task.Run(() => DoOtherThings());
  }
  ```

## 忽略织入(IgnoreMoAttribute)
在快速开始中，我们介绍了如何批量应用，由于批量引用的规则只限定了方法可访问性，所以可能有些符合规则的方法并不想应用织入，
此时便可使用`IgnoreMoAttribute`对指定方法/类进行标记，那么该方法/类(的所有方法)都将忽略织入。如果将`IgnoreMoAttribute`
应用到程序集(assembly)或模块(module)，那么该程序集(assembly)/模块(module)将忽略所有织入。另外，在应用`IgnoreMoAttribute`
时还可以通过MoTypes指定忽略的织入类型。
```csharp
// 当前程序集忽略所有织入
[assembly: IgnoreMo]
// 当前程序集忽略TheMoAttribute的织入
[assembly: IgnoreMo(MoTypes = new[] { typeof(TheMoAttribute))]

// 当前类忽略所有织入
[IgnoreMo]
class Class1
{
    // ...
}

// 当前类忽略TheMoAttribute的织入
[IgnoreMo(MoTypes = new[] { typeof(TheMoAttribute))]
class Class2
{
    // ...
}
```

## Attribute代理织入(MoProxyAttribute)
如果你已经使用一些第三方组件对一些方法进行了Attribute标记，现在你希望对这些标记过的方法进行aop操作，但又不想一个一个手动增加rougamo
的Attribute标记，此时便可以通过代理的方式一步完成aop织入。再比如你的项目现在有很多标记了`ObsoleteAttribute`的过时方法，你希望
在过期方法在被调用时输出调用堆栈日志，用来排查现在那些入口在使用这些过期方法，也可以通过该方式完成。
```csharp
public class ObsoleteProxyMoAttribute : MoAttribute
{
    public override void OnEntry(MethodContext context)
    {
        Log.Warning("过期方法被调用了：" + Environment.StackTrace);
    }
}

[module: MoProxy(typeof(ObsoleteAttribute), typeof(ObsoleteProxyMoAttribute))]

public class Cls
{
    [Obsolete]
    private int GetId()
    {
        // 该方法将应用织入代码
        return 123;
    }
}
```

## 织入互斥
### 单类型互斥(IRougamo<,>)
由于我们有Attribute标记和接口实现两种织入方式，那么就可能出现同时应用的情况，而如果两种织入的内容是相同的，那就会出现
重复织入的情况，为了尽量避免这种情况，在接口定义时，可以定义互斥类型，也就是同时只有一个能生效，具体哪个生效，根据
[优先级](#Priority)来定
```csharp
public class Mo1Attribute : MoAttribute
{
    // ...
}
public class Mo2Attribute : MoAttribute
{
    // ...
}
public class Mo3Attribute : MoAttribute
{
    // ...
}

public class Test : IRougamo<Mo1Attribute, Mo2Attribute>
{
    [Mo2]
    public void M1()
    {
        // Mo2Attribute应用于方法上，优先级高于接口实现的Mo1Attribute，Mo2Attribute将被应用
    }

    [Mo3]
    public void M2()
    {
        // Mo1Attribute和Mo3Attribute不互斥，两个都将被应用
    }
}
```

### 多类型互斥(IRepulsionsRougamo<,>)
`IRougamo<,>`只能与一个类型互斥，`IRepulsionsRougamo<,>`则可以与多个类型互斥
```csharp
public class Mo1Attribute : MoAttribute
{
}
public class Mo2Attribute : MoAttribute
{
}
public class Mo3Attribute : MoAttribute
{
}
public class Mo4Attribute : MoAttribute
{
}
public class Mo5Attribute : MoAttribute
{
}

public class TestRepulsion : MoRepulsion
{
    public override Type[] Repulsions => new[] { typeof(Mo2Attribute), typeof(Mo3Attribute) };
}

[assembly: Mo2]
[assembly: Mo5]

public class Class2 : IRepulsionsRougamo<Mo1Attribute, TestRepulsion>
{
    [Mo3]
    public void M1()
    {
        // Mo1与Mo2、Mo3互斥，但由于Mo3优先级高于Mo1，所以Mo1不生效时，所有互斥类型都将生效
        // 所以最终Mo2Attribute、Mo3Attribute、Mo5Attribute将被应用
        Console.WriteLine("m1");
    }

    [Mo4]
    public void M2()
    {
        // Mo1与Mo2、Mo3互斥，但由于Mo1优先级高于Mo2，所以Mo2将不生效
        // 最终Mo1Attribute、Mo4Attribute、Mo5Attribute将被应用
        Console.WriteLine("m2");
    }
}
```
<font color=red>通过上面的例子，你可能注意到，这个多类型互斥并不是多类型之间互相互斥，而是第一个泛型与第二个泛型定义的类型互斥，
第二个泛型之间并不互斥，也就像上面的示例那样，当`Mo1Attribute`不生效时，与它互斥的`Mo2Attribute`、`Mo3Attribute`都将生效。
这里需要理解，定义互斥的原因是Attribute和空接口实现两种方式可能存在的重复应用，而并不是为了排除所有织入的重复。同时也不推荐使用
多互斥定义，这样容易出现逻辑混乱，建议在应用织入前仔细思考一套统一的规则，而不是随意定义，然后试图使用多互斥来解决问题</font>

## 优先级(Priority)
1. `IgnoreMoAttribute`
2. Method `MoAttribute`
3. Method `MoProxyAttribute`
4. Type `MoAttribute`
5. Type `MoProxyAttribute`
6. Type `IRougamo<>`, `IRougamo<,>`, `IRepulsionsRougamo<,>`
7. Assembly & Module `MoAttribute`

## 开关(enable/disable)
Rougamo由个人开发，由于能力有限，对IL的研究也不是那么深入透彻，并且随着.NET的发展也会不断的出现一些新的类型、新的语意甚至新的IL指令，
也因此可能会存在一些BUG，而当IL层面的BUG可能无法快速定位到问题并修复，所以这里提供了一个开关可以在不去掉Rougamo引用的情况下不进行代码织入，
也因此推荐各位在使用Rougamo进行代码织入时，织入的代码是不影响业务的，比如日志和APM。如果希望使用稳定且在遇到问题能够快速得到支持的静态织入
组件，推荐使用[PostSharp](https://www.postsharp.net)

Rougamo是在[fody](https://github.com/Fody/Fody)的基础上研发的，引用Rougamo后首次编译会生成一个`FodyWeavers.xml`文件，默认内容如下
```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <Rougamo />
</Weavers>
```
在希望禁用Rougamo时，需要在配置文件的`Rougamo`节点增加属性`enabled`并设置值为`false`
```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <Rougamo enabled="false" />
</Weavers>
```

## 记录yield return IEnumerable/IAsyncEnumerable返回值
我们知道，使用`yield return`语法糖+`IEnumerable`返回值的方法，在调用方法结束后，该方法的代码实际并没有执行，代码的实际执行是在你访问
这个`IEnumerable`对象里的元素时候，比如你去foreach这个对象或者调用`ToList/ToArray`的时候，并且返回的这些元素并没有一个数组/链表进行
统一的保存（具体原理在这里不展开说明了），所以默认情况下是没有办法直接获取到`yield return IEnumerable`返回的所有元素集合的。

但可能有些对代码监控比较严格的场景需要记录所有返回值，所以在实现上我创建了一个数组保存了所有的返回元素，但是由于这个数组是额外创建的，会占
用额外的内存空间，同时又不清楚这个`IEnumerable`返回的元素集合有多大，所以为了避免可能额外产生过多的内存消耗，默认情况下是不会记录
`yield return IEnumerable`返回值的，如果需要记录返回值，需要在`FodyWeavers.xml`的`Rougamo`节点增加属性配置`enumerable-returns="true"`
```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <Rougamo enumerable-returns="true" />
</Weavers>
```

## 配置项
在引用Rougamo之后编译时会在项目根目录生成一个`FodyWeavers.xml`文件，格式如下：
```xml
<Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
  <Rougamo enabled="true" />
</Weavers>
```

下表中的配置项均配置到`FodyWeavers.xml`中。

|名称(~~曾用名~~)|默认值|说明|
|:------------:|:---:|:---|
|enabled|true|是否开启rougamo|
|composite-accessibility|false|是否使用类+方法综合可访问性进行匹配，默认仅按方法可访问性进行匹配。比如类的可访问性为internal，方法的可访问性为public，那么默认情况下该方法的可访问性认定为public，将该配置设置为true后，该方法的可访问性认定为internal|
|moarray-threshold|4|当方法上实际生效的`MoAttribute`达到该值时将使用数组保存。该配置用于优化织入代码，大多数情况下一个方法上仅一个`MoAttribute`，这时候使用数组保存在调用其方法时会产生更多的IL代码|
|iterator-returns(~~enumerable-returns~~)|false|是否保存iterator的返回值到`MethodContext.ReturnValue`，谨慎使用该功能，如果迭代器产生大量数据，启用该功能将占用等量内存|
|reverse-call-nonentry(~~reverse-call-ending~~)|true|当一个方法上有多个`MoAttribute`时，是否在执行`OnException/OnSuccess/OnExit`时按照`OnEntry`的倒序执行，默认倒序执行|
|except-type-patterns||类型全名称的正则表达式，符合该表达式的类型将被全局排除，多个正则表达式之间用英文逗号或者分号分隔|