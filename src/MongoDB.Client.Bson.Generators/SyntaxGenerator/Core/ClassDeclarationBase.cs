﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
namespace MongoDB.Client.Bson.Generators.SyntaxGenerator.Core
{
    internal abstract class ClassDeclarationBase
    {
        protected ClassDeclarationSyntax ClassDeclatation;
        protected ClassDeclMeta classDecl;
        protected TryParseMethodDeclatationBase TryParseMethod;
        internal INamedTypeSymbol ClassSymbol => classDecl.ClassSymbol;
        internal List<MemberDeclarationMeta> Members => classDecl.MemberDeclarations;
        public ClassDeclarationBase(ClassDeclMeta classdecl)
        {
            classDecl = classdecl;

        }
        public abstract MethodDeclarationSyntax DeclareTryParseMethod();

        public abstract TypeSyntax GetTryParseMethodOutParameter();
        public abstract TypeArgumentListSyntax GetInterfaceParameters();

        public virtual TypeParameterListSyntax GetTypeParametersList()
        {
            var paramsList = new SeparatedSyntaxList<TypeParameterSyntax>();
            foreach (var param in classDecl.ClassSymbol.TypeParameters)
            {
                paramsList = paramsList.Add(SF.TypeParameter(param.Name));
            }
            return SF.TypeParameterList(paramsList);
        }
        public SeparatedSyntaxList<BaseTypeSyntax> GetBaseList()
        {
            SeparatedSyntaxList<BaseTypeSyntax> list = new SeparatedSyntaxList<BaseTypeSyntax>();
            return list.Add(SF.SimpleBaseType(SF.GenericName(GeneratorBasics.SerializerInterface).WithTypeArgumentList(GetInterfaceParameters())));

        }
        public static ArrowExpressionClauseSyntax GenerateSpanNameValues(MemberDeclarationMeta memberdecl) //=> { byte, byte, byte, byte}
        {
            SeparatedSyntaxList<ExpressionSyntax> ArrayInitExpr(MemberDeclarationMeta memberdecl)
            {
                SeparatedSyntaxList<ExpressionSyntax> expr = new SeparatedSyntaxList<ExpressionSyntax>();
                foreach (var byteItem in Encoding.UTF8.GetBytes(memberdecl.StringFieldNameAlias))
                {
                    expr = expr.Add(GeneratorBasics.NumberLiteral(byteItem));
                }
                return expr;
            }
            ArrayRankSpecifierSyntax ArrayRank(MemberDeclarationMeta memberdecl)
            {
                return SF.ArrayRankSpecifier().AddSizes(GeneratorBasics.NumberLiteral(memberdecl.StringFieldNameAlias.Length));
            }

            return SF.ArrowExpressionClause(
                    SF.ArrayCreationExpression(
                        SF.ArrayType(
                           SF.ParseTypeName("byte"),
                           SF.SingletonList<ArrayRankSpecifierSyntax>(ArrayRank(memberdecl))),


                        SF.InitializerExpression(SyntaxKind.ArrayInitializerExpression, ArrayInitExpr(memberdecl))
                    )
                );
        }
        public SyntaxList<MemberDeclarationSyntax> GenerateStaticNamesSpans()
        {
            SyntaxList<MemberDeclarationSyntax> list = new SyntaxList<MemberDeclarationSyntax>();
            foreach (var memberdecl in classDecl.MemberDeclarations)
            {
                list = list.Add(
                   SF.PropertyDeclaration(
                       attributeLists: default,
                       modifiers: GeneratorBasics.PrivateStatic,
                       type: SF.ParseTypeName("ReadOnlySpan<byte>"),
                       explicitInterfaceSpecifier: default,
                       identifier: GeneratorBasics.GenerateReadOnlySpanNameSyntaxToken(ClassSymbol, memberdecl),
                       accessorList: default,
                       expressionBody: GenerateSpanNameValues(memberdecl),
                       initializer: default,
                       semicolonToken: SF.Token(SyntaxKind.SemicolonToken)
                    )
                );
            }
            return list;
        }
        public abstract ClassDeclarationSyntax Build();
    }
}
