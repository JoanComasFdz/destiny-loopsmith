using System.Collections.Immutable;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Loopsmith.Core.Tests.Architecture;

/// <summary>A referenced type: <see cref="Namespace"/> is that of its outermost declaring type.</summary>
internal sealed record TypeDependency(string Namespace, string FullName)
{
    public bool IsIn(string @namespace) =>
        Namespace == @namespace || Namespace.StartsWith(@namespace + ".", StringComparison.Ordinal);
}

/// <summary>
/// Every type one type definition references: signatures, base types, interfaces, generic constraints and
/// arguments, attributes (including <c>typeof</c> arguments), locals, catch clauses and every IL operand.
/// Nested types are walked separately; callers attribute them to the outermost type.
/// </summary>
internal static class TypeDependencies
{
    public static ImmutableHashSet<TypeDependency> CollectDependencies(
        TypeDefinition type,
        Func<CustomAttribute, bool> canDecodeArguments)
    {
        var found = new HashSet<TypeDependency>();

        AddAttributes(type);
        AddType(type.BaseType);
        foreach (var implemented in type.Interfaces)
        {
            AddType(implemented.InterfaceType);
            AddAttributes(implemented);
        }

        AddGenericParameters(type);

        foreach (var field in type.Fields)
        {
            AddType(field.FieldType);
            AddAttributes(field);
        }

        foreach (var property in type.Properties)
        {
            AddType(property.PropertyType);
            AddAttributes(property);
        }

        foreach (var @event in type.Events)
        {
            AddType(@event.EventType);
            AddAttributes(@event);
        }

        foreach (var method in type.Methods)
        {
            AddMethodDefinition(method);
        }

        return [.. found];

        void AddMethodDefinition(MethodDefinition method)
        {
            AddAttributes(method);
            AddAttributes(method.MethodReturnType);
            AddType(method.ReturnType);
            foreach (var parameter in method.Parameters)
            {
                AddType(parameter.ParameterType);
                AddAttributes(parameter);
            }

            AddGenericParameters(method);
            foreach (var overridden in method.Overrides)
            {
                AddMethod(overridden);
            }

            if (!method.HasBody)
            {
                return;
            }

            foreach (var variable in method.Body.Variables)
            {
                AddType(variable.VariableType);
            }

            foreach (var handler in method.Body.ExceptionHandlers)
            {
                AddType(handler.CatchType);
            }

            foreach (var instruction in method.Body.Instructions)
            {
                AddOperand(instruction.Operand);
            }
        }

        void AddOperand(object? operand)
        {
            switch (operand)
            {
                case TypeReference typeReference:
                    AddType(typeReference);
                    break;
                case MethodReference methodReference:
                    AddMethod(methodReference);
                    break;
                case FieldReference fieldReference:
                    AddType(fieldReference.DeclaringType);
                    AddType(fieldReference.FieldType);
                    break;
                case CallSite callSite:
                    AddType(callSite.ReturnType);
                    foreach (var parameter in callSite.Parameters)
                    {
                        AddType(parameter.ParameterType);
                    }

                    break;
            }
        }

        void AddMethod(MethodReference method)
        {
            AddType(method.DeclaringType);
            AddType(method.ReturnType);
            foreach (var parameter in method.Parameters)
            {
                AddType(parameter.ParameterType);
            }

            if (method is GenericInstanceMethod generic)
            {
                foreach (var argument in generic.GenericArguments)
                {
                    AddType(argument);
                }
            }
        }

        void AddGenericParameters(IGenericParameterProvider provider)
        {
            foreach (var parameter in provider.GenericParameters)
            {
                AddAttributes(parameter);
                foreach (var constraint in parameter.Constraints)
                {
                    AddType(constraint.ConstraintType);
                }
            }
        }

        void AddAttributes(ICustomAttributeProvider provider)
        {
            foreach (var attribute in provider.CustomAttributes)
            {
                AddType(attribute.AttributeType);
                if (!canDecodeArguments(attribute))
                {
                    continue;
                }

                foreach (var argument in attribute.ConstructorArguments)
                {
                    AddAttributeArgument(argument);
                }

                foreach (var named in attribute.Fields.Concat(attribute.Properties))
                {
                    AddAttributeArgument(named.Argument);
                }
            }
        }

        void AddAttributeArgument(CustomAttributeArgument argument)
        {
            AddType(argument.Type);
            switch (argument.Value)
            {
                case TypeReference typeOf:
                    AddType(typeOf);
                    break;
                case CustomAttributeArgument boxed:
                    AddAttributeArgument(boxed);
                    break;
                case CustomAttributeArgument[] array:
                    foreach (var element in array)
                    {
                        AddAttributeArgument(element);
                    }

                    break;
            }
        }

        void AddType(TypeReference? reference)
        {
            switch (reference)
            {
                case null:
                case GenericParameter:
                    return;   // a generic parameter's constraints are collected where it is declared
                case GenericInstanceType generic:
                    AddType(generic.ElementType);
                    foreach (var argument in generic.GenericArguments)
                    {
                        AddType(argument);
                    }

                    return;
                case RequiredModifierType required:
                    AddType(required.ModifierType);
                    AddType(required.ElementType);
                    return;
                case OptionalModifierType optional:
                    AddType(optional.ModifierType);
                    AddType(optional.ElementType);
                    return;
                case FunctionPointerType pointer:
                    AddType(pointer.ReturnType);
                    foreach (var parameter in pointer.Parameters)
                    {
                        AddType(parameter.ParameterType);
                    }

                    return;
                case TypeSpecification specification:   // array, by-ref, pointer, pinned, sentinel
                    AddType(specification.ElementType);
                    return;
                default:
                    found.Add(new TypeDependency(FindHomeNamespace(reference), reference.FullName));
                    return;
            }
        }
    }

    private static string FindHomeNamespace(TypeReference reference)
    {
        var current = reference;
        while (current.DeclaringType is not null)
        {
            current = current.DeclaringType;
        }

        return current.Namespace;
    }
}
