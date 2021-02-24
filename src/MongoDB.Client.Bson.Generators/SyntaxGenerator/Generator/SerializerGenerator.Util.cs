﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
namespace MongoDB.Client.Bson.Generators.SyntaxGenerator.Generator
{
    public static class ImmutableListBuilderExt
    {
        public static ImmutableList<StatementSyntax> ToStatements<T>(this ImmutableList<T> source)
            where T : ExpressionSyntax
        {
            return source.Select(x => SF.ExpressionStatement(x)).ToImmutableList<StatementSyntax>();
        }
        public static void Add(this ImmutableList<StatementSyntax>.Builder builder, ExpressionSyntax expr)
        {
            builder.Add(SerializerGenerator.Statement(expr));
        }
        public static void IfStatement(this ImmutableList<StatementSyntax>.Builder builder, ExpressionSyntax condition, StatementSyntax statement, BlockSyntax @else)
        {
            builder.Add(SerializerGenerator.IfStatement(condition, statement, @else));
        }
        public static void IfStatement(this ImmutableList<StatementSyntax>.Builder builder, ExpressionSyntax condition, StatementSyntax statement)
        {
            builder.Add(SerializerGenerator.IfStatement(condition, statement));
        }
        public static void IfNot(this ImmutableList<StatementSyntax>.Builder builder, ExpressionSyntax condition, params StatementSyntax[] statement)
        {
            builder.Add(SerializerGenerator.IfNot(condition, statement));
        }
        public static void DefaultLocalDeclarationStatement(this ImmutableList<StatementSyntax>.Builder builder, TypeSyntax type, SyntaxToken variable)
        {
            builder.Add(SerializerGenerator.DefaultLocalDeclarationStatement(type, variable));
        }
        public static void TryReadGeneric(this ImmutableList<StatementSyntax>.Builder builder, SyntaxToken bsonType, ExpressionSyntax assignOrDecl)
        {
            builder.Add(SerializerGenerator.TryReadGeneric(bsonType, assignOrDecl));
        }
        public static void TryReadGenericNullable(this ImmutableList<StatementSyntax>.Builder builder, TypeSyntax typeParam, SyntaxToken bsonType, ExpressionSyntax assignOrDecl)
        {
            builder.Add(SerializerGenerator.TryReadGenericNullable(typeParam, bsonType, assignOrDecl));
        }
    }
}