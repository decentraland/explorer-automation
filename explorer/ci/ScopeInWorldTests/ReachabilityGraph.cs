using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ExplorerAutomation.Ci.ScopeInWorldTests;

/// <summary>
/// Per-fixture reachability over member symbols: which source files can a given
/// <c>[Category("InWorld")]</c> fixture actually reach.
/// </summary>
internal sealed class ReachabilityGraph
{
    private readonly Compilation _compilation;
    private readonly string _repoRoot;
    private readonly Dictionary<SyntaxTree, SemanticModel> _models = new();
    private readonly Dictionary<ISymbol, List<ISymbol>> _edges = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, List<ISymbol>> _declarations = new(SymbolEqualityComparer.Default);

    /// <summary>InWorld fixture names, sorted.</summary>
    public List<string> Fixtures { get; } = [];

    /// <summary>Repo-relative paths of every file in the compilation.</summary>
    public HashSet<string> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>fixture name → (reachable file → member path that reaches it).</summary>
    public Dictionary<string, Dictionary<string, string>> Reach { get; } = new(StringComparer.Ordinal);

    /// <summary>Why the closure must not be believed, when something about the tree invalidates it.</summary>
    public string? Untrusted { get; private set; }

    private ReachabilityGraph(Compilation compilation, string repoRoot)
    {
        _compilation = compilation;
        _repoRoot = repoRoot;
    }

    public static ReachabilityGraph Build(Compilation compilation, string repoRoot)
    {
        var graph = new ReachabilityGraph(compilation, repoRoot);

        foreach (var tree in compilation.SyntaxTrees)
            graph.Files.Add(graph.Relative(tree.FilePath));

        foreach (var fixture in graph.FindInWorldFixtures())
        {
            // Reach and the ALL check are keyed on the simple name, and the vstest filter
            // cannot separate two fixtures that share one either.
            if (!graph.Reach.TryAdd(fixture.Name, graph.Closure(fixture)))
                graph.Untrusted ??= $"two InWorld fixtures are both named {fixture.Name}";
            else
                graph.Fixtures.Add(fixture.Name);
        }

        graph.Untrusted ??= graph.FindMethodLevelCategory();
        graph.Fixtures.Sort(StringComparer.Ordinal);
        return graph;
    }

    #region Fixture discovery

    /// <summary>
    /// NUnit applies a base class's categories to every derived fixture, so the attribute is
    /// resolved through the base chain rather than read off the fixture's own declaration.
    /// </summary>
    private IEnumerable<INamedTypeSymbol> FindInWorldFixtures()
    {
        foreach (var type in AllTypes(_compilation.Assembly.GlobalNamespace))
        {
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsStatic)
                continue;
            if (HasInWorldCategory(type))
                yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> AllTypes(INamespaceOrTypeSymbol scope)
    {
        foreach (var member in scope.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol ns:
                    foreach (var t in AllTypes(ns)) yield return t;
                    break;
                case INamedTypeSymbol type:
                    yield return type;
                    foreach (var t in AllTypes(type)) yield return t;
                    break;
            }
        }
    }

    private static bool HasInWorldCategory(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.BaseType)
            foreach (var attribute in t.GetAttributes())
                if (IsCategory(attribute.AttributeClass)
                    && attribute.ConstructorArguments.Length == 1
                    && attribute.ConstructorArguments[0].Value as string == "InWorld")
                    return true;

        return false;
    }

    private static bool IsCategory(INamedTypeSymbol? attributeClass)
    {
        for (var t = attributeClass; t is not null; t = t.BaseType)
            if (t.Name == "CategoryAttribute"
                && t.ContainingNamespace?.ToDisplayString().StartsWith("NUnit", StringComparison.Ordinal) == true)
                return true;

        return false;
    }

    /// <summary>
    /// A method-level category puts individual tests in the vstest filter without putting
    /// their fixture in <see cref="Fixtures"/>, so the closure would under-select. None exist
    /// today; if one appears, the caller must run everything.
    /// </summary>
    private string? FindMethodLevelCategory()
    {
        foreach (var type in AllTypes(_compilation.Assembly.GlobalNamespace))
            foreach (var method in type.GetMembers().OfType<IMethodSymbol>())
                foreach (var attribute in method.GetAttributes())
                    if (IsCategory(attribute.AttributeClass))
                        return $"{type.Name}.{method.Name} carries a method-level [Category]";

        return null;
    }

    #endregion

    #region Closure

    /// <summary>
    /// Breadth-first closure seeded from the fixture's own declaration, keeping the first
    /// (shortest) member path that reaches each file for the audit trail.
    /// </summary>
    private Dictionary<string, string> Closure(INamedTypeSymbol fixture)
    {
        var parent = new Dictionary<ISymbol, ISymbol?>(SymbolEqualityComparer.Default) { [fixture] = null };
        var queue = new Queue<ISymbol>();
        var reached = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Record(fixture, fixture);

        // The fixture is entered through its declaration syntax, not through the type rule
        // below, so its test bodies are the roots of the closure. The base chain is seeded
        // the same way: NUnit runs the inherited [OneTimeSetUp]/[SetUp] members by
        // reflection, so nothing in the tree names EnsureInWorld or the boot views it drives.
        for (INamedTypeSymbol? type = fixture; type is not null && IsLocal(type); type = type.BaseType)
        {
            if (!SymbolEqualityComparer.Default.Equals(type, fixture))
                Visit(type, fixture);

            foreach (var next in DeclarationEdges(type))
                Visit(next, type);
        }

        while (queue.Count > 0)
        {
            var symbol = queue.Dequeue();
            foreach (var next in EdgesOf(symbol))
                Visit(next, symbol);
        }

        return reached;

        void Visit(ISymbol symbol, ISymbol from)
        {
            if (!parent.TryAdd(symbol, from))
                return;

            Record(symbol, from);
            queue.Enqueue(symbol);
        }

        void Record(ISymbol symbol, ISymbol from)
        {
            foreach (var reference in symbol.DeclaringSyntaxReferences)
            {
                var file = Relative(reference.SyntaxTree.FilePath);
                if (!reached.ContainsKey(file))
                    reached[file] = MemberPath(symbol, parent);
            }
        }
    }

    private static string MemberPath(ISymbol symbol, Dictionary<ISymbol, ISymbol?> parent)
    {
        var hops = new List<string>();
        for (ISymbol? s = symbol; s is not null; s = parent.GetValueOrDefault(s))
        {
            hops.Add(Describe(s));
            if (!parent.TryGetValue(s, out var next) || next is null || SymbolEqualityComparer.Default.Equals(next, s))
                break;
        }

        hops.Reverse();
        return string.Join(" -> ", hops);
    }

    private static string Describe(ISymbol symbol) =>
        symbol is INamedTypeSymbol type
            ? type.Name
            : $"{symbol.ContainingType?.Name}.{symbol.Name}";

    #endregion

    #region Edges

    private List<ISymbol> EdgesOf(ISymbol symbol)
    {
        if (_edges.TryGetValue(symbol, out var cached))
            return cached;

        // Named types contribute their base chain only. Enumerating their members instead
        // would make ViewContainer and ExplorePanelView link every fixture to every view.
        var edges = symbol is INamedTypeSymbol type
            ? TypeEdges(type.BaseType).ToList()
            : DeclarationEdges(symbol);

        _edges[symbol] = edges;
        return edges;
    }

    // Cached separately from EdgesOf: base classes are walked once per derived fixture.
    private List<ISymbol> DeclarationEdges(ISymbol symbol)
    {
        if (_declarations.TryGetValue(symbol, out var cached))
            return cached;

        var edges = WalkDeclarations(symbol).ToList();
        _declarations[symbol] = edges;
        return edges;
    }

    private IEnumerable<ISymbol> WalkDeclarations(ISymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            var model = ModelFor(declaration.SyntaxTree);

            foreach (var node in Descend(declaration))
            {
                if (node is ExpressionSyntax)
                {
                    var info = model.GetSymbolInfo(node);
                    var referenced = info.Symbol
                                     ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);
                    if (referenced is not null)
                        foreach (var edge in Edges(referenced))
                            yield return edge;
                }

                if (node is MemberDeclarationSyntax or VariableDeclaratorSyntax or LocalFunctionStatementSyntax)
                {
                    var declared = model.GetDeclaredSymbol(node);
                    if (declared is not null && !SymbolEqualityComparer.Default.Equals(declared, symbol))
                        foreach (var edge in Edges(declared))
                            yield return edge;
                }
            }
        }
    }

    /// <summary>Turns one resolved symbol reference into the graph nodes it implies.</summary>
    // Binding is static, so an override is not followed from a base-typed call. Its file is
    // still selected: every view instance is reached through a concrete-typed member, and
    // that member's type is an edge.
    private IEnumerable<ISymbol> Edges(ISymbol symbol)
    {
        switch (symbol)
        {
            case IMethodSymbol method:
                var definition = (method.ReducedFrom ?? method).OriginalDefinition;
                // Constructors are never nodes: view trees are built in constructors, so a
                // single edge into one would pull in every view the constructor instantiates.
                if (definition.MethodKind is MethodKind.Constructor
                        or MethodKind.StaticConstructor
                        or MethodKind.Destructor)
                    break;
                if (definition.AssociatedSymbol is { } associated)
                {
                    foreach (var edge in Edges(associated)) yield return edge;
                    break;
                }

                if (IsLocal(definition)) yield return definition;
                foreach (var edge in TypeEdges(definition.ReturnType)) yield return edge;
                foreach (var parameter in definition.Parameters)
                    foreach (var edge in TypeEdges(parameter.Type)) yield return edge;
                break;

            case IPropertySymbol property:
                if (IsLocal(property.OriginalDefinition)) yield return property.OriginalDefinition;
                foreach (var edge in TypeEdges(property.Type)) yield return edge;
                break;

            case IFieldSymbol field:
                if (IsLocal(field.OriginalDefinition)) yield return field.OriginalDefinition;
                foreach (var edge in TypeEdges(field.Type)) yield return edge;
                break;

            case IEventSymbol @event:
                if (IsLocal(@event.OriginalDefinition)) yield return @event.OriginalDefinition;
                foreach (var edge in TypeEdges(@event.Type)) yield return edge;
                break;

            // A parameter or local is not a node of its own; only the type it names is.
            case IParameterSymbol parameter:
                foreach (var edge in TypeEdges(parameter.Type)) yield return edge;
                break;

            case ILocalSymbol local:
                foreach (var edge in TypeEdges(local.Type)) yield return edge;
                break;

            case ITypeSymbol type:
                foreach (var edge in TypeEdges(type)) yield return edge;
                break;
        }
    }

    private IEnumerable<ISymbol> TypeEdges(ITypeSymbol? type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (var edge in TypeEdges(array.ElementType)) yield return edge;
                break;

            case INamedTypeSymbol named:
                if (IsLocal(named.OriginalDefinition)) yield return named.OriginalDefinition;
                foreach (var argument in named.TypeArguments)
                    foreach (var edge in TypeEdges(argument)) yield return edge;
                break;
        }
    }

    private bool IsLocal(ISymbol symbol) =>
        symbol.DeclaringSyntaxReferences.Length > 0
        && SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, _compilation.Assembly);

    #endregion

    #region Syntax traversal

    private static IEnumerable<SyntaxNode> Descend(SyntaxNode root)
    {
        var stack = new Stack<SyntaxNode>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node != root && IsWiring(node))
                continue;

            yield return node;
            foreach (var child in node.ChildNodes())
                stack.Push(child);
        }
    }

    /// <summary>
    /// Constructors and member initializers are the wiring that assembles the view tree.
    /// Skipping them is what keeps a fixture's closure to the members it actually touches;
    /// nothing is lost because selection is per file and the wiring shares a file with the
    /// members that read what it built.
    /// </summary>
    private static bool IsWiring(SyntaxNode node) => node switch
    {
        ConstructorDeclarationSyntax => true,
        ConstructorInitializerSyntax => true,
        ArgumentListSyntax { Parent: PrimaryConstructorBaseTypeSyntax } => true,
        EqualsValueClauseSyntax clause => clause.Parent switch
        {
            PropertyDeclarationSyntax => true,
            VariableDeclaratorSyntax { Parent.Parent: FieldDeclarationSyntax or EventFieldDeclarationSyntax } => true,
            _ => false,
        },
        _ => false,
    };

    private SemanticModel ModelFor(SyntaxTree tree)
    {
        if (!_models.TryGetValue(tree, out var model))
            _models[tree] = model = _compilation.GetSemanticModel(tree);

        return model;
    }

    #endregion

    private string Relative(string? path) =>
        string.IsNullOrEmpty(path)
            ? string.Empty
            : Path.GetRelativePath(_repoRoot, path).Replace('\\', '/');
}
