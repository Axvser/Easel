using Microsoft.CodeAnalysis;

namespace Easel.Generator;

/// <summary>The diagnostics this generator reports. IDs are stable once released.</summary>
internal static class Diagnostics
{
    private const string XamlCategory = "Easel.Xaml";
    private const string PropertyCategory = "Easel.Xaml";

    public static readonly DiagnosticDescriptor UnknownType = new(
        "EASEL_XAML001", "Unknown XAML type",
        "The XAML type '{0}' could not be resolved",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownMember = new(
        "EASEL_XAML002", "Unknown XAML member",
        "'{0}' has no property or event named '{1}'",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedValue = new(
        "EASEL_XAML003", "Unsupported XAML value",
        "The value '{0}' cannot be converted to '{1}'",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedConstruct = new(
        "EASEL_XAML004", "Unsupported XAML construct",
        "{0}",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingClass = new(
        "EASEL_XAML005", "Missing x:Class",
        "A XAML file consumed by this generator needs an x:Class attribute on its root element",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ClassNotFound = new(
        "EASEL_XAML006", "x:Class type not found",
        "The x:Class type '{0}' was not found in the compilation",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateName = new(
        "EASEL_XAML007", "Duplicate x:Name",
        "The name '{0}' is already a member of '{1}'",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingEventHandler = new(
        "EASEL_XAML008", "Missing event handler",
        "The event handler '{0}' was not found in '{1}'",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotPartial = new(
        "EASEL_XAML009", "x:Class type is not partial",
        "The x:Class type '{0}' must be declared partial",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedResource = new(
        "EASEL_XAML010", "Unsupported resource",
        "{0}",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedRoot = new(
        "EASEL_XAML011", "Unsupported root",
        "The root element '{0}' is not assignable from '{1}'",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ParseError = new(
        "EASEL_XAML012", "XAML parse error",
        "The XAML file could not be parsed: {0}",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownAttachedProperty = new(
        "EASEL_XAML013", "Unknown attached property",
        "The attached property '{0}.{1}' was not found; declare a static {1}Property field or a [XamlAttachedProperty] accessor",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidStyleTargetType = new(
        "EASEL_XAML014", "Invalid style TargetType",
        "{0}",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InitializeComponentAlreadyDeclared = new(
        "EASEL_XAML015", "InitializeComponent already declared",
        "'{0}' already declares InitializeComponent; the XAML generator emits a private non-partial method and will not override an existing member",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AmbiguousEventHandler = new(
        "EASEL_XAML016", "Ambiguous event handler",
        "The event handler '{0}' is ambiguous in '{1}'; rename it so only one method matches",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidEventHandlerSignature = new(
        "EASEL_XAML017", "Invalid event handler signature",
        "The event handler '{0}' does not match the '{1}' event signature",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AmbiguousType = new(
        "EASEL_XAML018", "Ambiguous XAML type",
        "The XAML type '{0}' is ambiguous: both '{1}' and '{2}' define it; use an explicit prefix (for example 'local:{0}')",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnprefixedTypeNotEasel = new(
        "EASEL_XAML019", "Unprefixed type is not an Easel type",
        "The unprefixed type '{0}' is not an Easel visual type; unprefixed names resolve to the visual backend, so write 'local:{0}' for a type in your own namespace",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CompiledBindingNeedsDataType = new(
        "EASEL_XAML020", "Compiled binding needs x:DataType",
        "x:CompileBindings is enabled but no x:DataType is in scope for the binding path '{0}'",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownCompiledBindingMember = new(
        "EASEL_XAML021", "Unknown compiled binding member",
        "'{0}' has no accessible property named '{1}' for the compiled binding path",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ReadOnlyCompiledBindingLeaf = new(
        "EASEL_XAML022", "Compiled binding leaf is read-only",
        "'{0}' cannot be written by a {1} compiled binding",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidBindingDirective = new(
        "EASEL_XAML023", "Invalid binding directive",
        "The x:{0} value '{1}' is not valid",
        XamlCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnsupportedCompiledBindingPath = new(
        "EASEL_XAML024", "Unsupported compiled binding path",
        "The compiled binding path '{0}' is not supported ({1}); the binding falls back to reflection",
        XamlCategory, DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyNotPartial = new(
        "EASEL_XAMLP001", "XamlProperty must be partial",
        "A [XamlProperty] property must be declared partial and have no accessor bodies",
        PropertyCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyOwnerUnsupported = new(
        "EASEL_XAMLP002", "XamlProperty owner unsupported",
        "[XamlProperty] requires a partial type derived from Easel.Visuals.VisualElement",
        PropertyCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor AttachedInvalid = new(
        "EASEL_XAMLP003", "Invalid attached property accessors",
        "{0}",
        PropertyCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyNameConflict = new(
        "EASEL_XAMLP004", "Dependency property name conflict",
        "'{0}' already declares a member named '{1}Property'",
        PropertyCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PropertyDefaultValueRequired = new(
        "EASEL_XAMLP005", "Dependency property default value required",
        "The dependency property '{0}' is typed '{1}' (non-nullable reference); set DefaultValue or declare it nullable",
        PropertyCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ChangedCallbackInvalid = new(
        "EASEL_XAMLP006", "Invalid changed callback",
        "{0}",
        PropertyCategory, DiagnosticSeverity.Error, isEnabledByDefault: true);
}
