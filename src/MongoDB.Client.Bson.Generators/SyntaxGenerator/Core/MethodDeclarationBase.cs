﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace MongoDB.Client.Bson.Generators.SyntaxGenerator.Core
{
    internal abstract class MethodDeclarationBase
    {
        protected List<MemberDeclarationMeta> Members;
        public INamedTypeSymbol ClassSymbol;
        public MethodDeclarationBase(INamedTypeSymbol classSymbol, List<MemberDeclarationMeta> members)
        {
            ClassSymbol = classSymbol;
            Members = members;
        }
        public abstract ExplicitInterfaceSpecifierSyntax ExplicitInterfaceSpecifier();
        public abstract BlockSyntax GenerateMethodBody();

        public abstract MethodDeclarationSyntax DeclareMethod();
    }
}
