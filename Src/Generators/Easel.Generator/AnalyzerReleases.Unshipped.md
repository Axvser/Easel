; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
EASEL_XAML001 | Easel.Xaml | Error | Unknown XAML type
EASEL_XAML002 | Easel.Xaml | Error | Unknown XAML member
EASEL_XAML003 | Easel.Xaml | Warning | Unsupported XAML value
EASEL_XAML004 | Easel.Xaml | Warning | Unsupported XAML construct
EASEL_XAML005 | Easel.Xaml | Error | Missing x:Class
EASEL_XAML006 | Easel.Xaml | Error | x:Class type not found
EASEL_XAML007 | Easel.Xaml | Warning | Duplicate x:Name
EASEL_XAML008 | Easel.Xaml | Warning | Missing event handler
EASEL_XAML009 | Easel.Xaml | Error | x:Class type is not partial
EASEL_XAML010 | Easel.Xaml | Warning | Unsupported resource
EASEL_XAML011 | Easel.Xaml | Warning | Unsupported root
EASEL_XAML012 | Easel.Xaml | Error | XAML parse error
EASEL_XAML013 | Easel.Xaml | Warning | Unknown attached property
EASEL_XAML014 | Easel.Xaml | Error | Invalid style TargetType
EASEL_XAML015 | Easel.Xaml | Error | InitializeComponent already declared
EASEL_XAML016 | Easel.Xaml | Warning | Ambiguous event handler
EASEL_XAML017 | Easel.Xaml | Warning | Invalid event handler signature
EASEL_XAML018 | Easel.Xaml | Error | Ambiguous XAML type
EASEL_XAML019 | Easel.Xaml | Error | Unprefixed type is not a Easel type
EASEL_XAMLP001 | Easel.Xaml | Error | XamlProperty must be partial
EASEL_XAMLP002 | Easel.Xaml | Error | XamlProperty owner unsupported
EASEL_XAMLP003 | Easel.Xaml | Error | Invalid attached property accessors
EASEL_XAMLP004 | Easel.Xaml | Error | Dependency property name conflict
EASEL_XAMLP005 | Easel.Xaml | Error | Dependency property default value required
EASEL_XAMLP006 | Easel.Xaml | Error | Invalid changed callback
