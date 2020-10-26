﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MongoDB.Client.Bson.Generators.SyntaxGenerator.Core;
using MongoDB.Client.Bson.Generators.SyntaxGenerator.Methods;
using System;
using System.Collections.Generic;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
namespace MongoDB.Client.Bson.Generators.SyntaxGenerator
{
    internal class GenericClassDeclaration : ClassDeclarationBase
    {
        public GenericClassDeclaration(ClassDeclMeta classdecl) : base(classdecl)
        {

        }

        public SeparatedSyntaxList<TypeSyntax> GetGenericParametersList()
        {
            var genericsParameters = new SeparatedSyntaxList<TypeSyntax>();
            for (var index = 0; index < classDecl.ClassSymbol.TypeArguments.Length; index++)
            {
                genericsParameters = genericsParameters.Add(SF.ParseTypeName(classDecl.ClassSymbol.TypeArguments[index].Name));
            }
            return genericsParameters;
        }

        public override TypeArgumentListSyntax GetInterfaceParameters()
        {
            var genericsParameters = new SeparatedSyntaxList<TypeSyntax>();
            for (var index = 0; index < ClassSymbol.TypeArguments.Length; index++)
            {
                genericsParameters = genericsParameters.Add(SF.ParseTypeName(ClassSymbol.TypeArguments[index].Name));
            }
            return SF.TypeArgumentList().AddArguments(SF.GenericName(ClassSymbol.Name).WithTypeArgumentList(SF.TypeArgumentList(genericsParameters)));
        }

        public override TypeSyntax GetTryParseMethodOutParameter()
        {
            return SF.GenericName(SF.ParseToken(ClassSymbol.Name),
                                  SF.TypeArgumentList(GetGenericParametersList()));
        }

        public override MethodDeclarationSyntax DeclareTryParseMethod()
        {
            return new GenericTryParseMethodDeclaration(this).DeclareTryParseMethod();
        }
        public override ClassDeclarationSyntax Build()
        {
            var decl = SF.ClassDeclaration(GeneratorBasics.GenerateSerializerName(ClassSymbol));
            decl = decl.WithTypeParameterList(GetTypeParametersList());
            return decl.WithBaseList(SF.BaseList(GetBaseList()))
                       .WithMembers(GenerateStaticNamesSpans())
                       .AddMembers(DeclareTryParseMethod());
        }
    }
}
