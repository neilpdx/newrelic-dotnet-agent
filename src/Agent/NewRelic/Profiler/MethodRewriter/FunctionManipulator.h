/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
#pragma once
#include <functional>
#include <stdint.h>
#include <sstream>
#include <algorithm>
#include <unordered_map>
#include "../Common/Macros.h"
#include "Exceptions.h"
#include "../Common/CorStandIn.h"
#include "IFunction.h"
#include "ISystemCalls.h"
#include "InstructionSet.h"
#include "InstantiatedGenericType.h"
#include "ExceptionHandlerManipulator.h"
#include "../Logging/Logger.h"
#include "../Configuration/InstrumentationPoint.h"
#include "../Sicily/codegen/ByteCodeGenerator.h"
#include "../SignatureParser/SignatureParser.h"
#include "IFunctionHeaderInfo.h"

namespace NewRelic { namespace Profiler { namespace MethodRewriter
{
// Test unsafe rethrows exceptions throw while we create and finish tracers so you
// can debug what's being swallowed.
//#define TEST_UNSAFE 1

    class FunctionManipulator
    {
    protected:
        enum Scope
        {
            THREAD_LOCAL,
            APP_DOMAIN
        };

        IFunctionPtr _function;
        InstructionSetPtr _instructions;
        ExceptionHandlerManipulatorPtr _exceptionHandlerManipulator;
        ByteVector _newHeader;
        ByteVector _oldCodeBytes;
        ByteVector _newLocalVariablesSignature;
        SignatureParser::MethodSignaturePtr _methodSignature;

    public:
        FunctionManipulator(IFunctionPtr function) :
            _function(function),
            _newHeader(sizeof(COR_ILMETHOD_FAT)),
            _methodSignature(SignatureParser::SignatureParser::ParseMethodSignature(function->GetSignature()->begin(), function->GetSignature()->end()))
        {
        }

    protected:
        void Initialize() {
            ExtractHeaderBodyAndExtra();
            ExtractLocalVariablesSignature();
            _instructions = std::make_shared<InstructionSet>(_function->GetTokenizer(), _exceptionHandlerManipulator);
        }

        // rewrite this method with something else; handle FatalFunctionManipulatorException specially!
        void Instrument()
        {
            LogTrace(_function->ToString(), L": Writing generated bytecode.");

            // set the code size in the header
            GetHeader()->SetCodeSize(uint32_t(_instructions->GetBytes().size()));

            // set the extra section flag in the header
            GetHeader()->SetFlags(GetHeader()->GetFlags() | CorILMethod_MoreSects);

            // write the locals to the header
            WriteLocalsToHeader();

            // combine the header, instructions and extra sections into a single ByteVector
            auto newMethod = CombineHeaderInstructionsAndExtraSections();

            // write the new method to the function so it can be JIT compiled; this is the part that could be fatal
            try
            {
                LogTrace(_function->ToString(), L": Writing method bytes to method for JIT compilation.");
                _function->WriteMethod(newMethod);
            }
            catch (...)
            {
                LogError(_function->ToString(), L": A potentially fatal error occurred when attempting to instrument, this function may no longer be valid.");
                throw;
            }
        }

        void InstrumentTiny()
        {
            LogTrace(_function->ToString(), L": Writing generated bytecode.");

            // re-initialize the header as a tiny header
            _newHeader.resize(sizeof(COR_ILMETHOD_TINY));
            std::fill(_newHeader.begin(), _newHeader.end(), static_cast<uint8_t>(0));
            COR_ILMETHOD_TINY* tinyHeader = (COR_ILMETHOD_TINY*)_newHeader.data();
            
            // set the 8 bits that make up the tiny header
            auto tinyFlag = 0x2;
            auto codeSize = _instructions->GetBytes().size();
            tinyHeader->Flags_CodeSize = (uint8_t)((codeSize << 2) | tinyFlag);

            // combine the header and the method body
            ByteVector newMethodBytes;
            AppendHeaderBytes(newMethodBytes);
            AppendInstructionBytes(newMethodBytes);

            // write the new method to the function so it can be JIT compiled; this is the part that could be fatal
            try
            {
                LogTrace(_function->ToString(), L": Writing method bytes to method for JIT compilation.");
                _function->WriteMethod(newMethodBytes);
            }
            catch (...)
            {
                LogError(_function->ToString(), L": A potentially fatal error occurred when attempting to instrument, this function may no longer be valid.");
                throw;
            }
        }

        // extract the header and body of the method into member variables, converting the header to fat along the way if necessary
        void ExtractHeaderBodyAndExtra()
        {
            LogTrace(_function->ToString(), L": Breaking up the bytes into header, code and extra sections.");

            auto originalMethodBytes = _function->GetMethodBytes();

            uint8_t* header = originalMethodBytes->data();
            COR_ILMETHOD_TINY* tinyHeader = (COR_ILMETHOD_TINY*)header;
            COR_ILMETHOD_FAT* fatHeader = (COR_ILMETHOD_FAT*)header;

            // fat headers can just be extracted1>------ Build started: Project: Profiler, Configuration: Debug x64 ------
            if (fatHeader->IsFat())
            {
                uint8_t* headerBegin = &originalMethodBytes->front();
                uint8_t* headerEnd = headerBegin + sizeof(COR_ILMETHOD_FAT);
                uint8_t* codeBegin = fatHeader->GetCode();
                uint8_t* codeEnd = codeBegin + fatHeader->GetCodeSize();

                bool hasExtraSections = fatHeader->More();

                _newHeader.assign(headerBegin, headerEnd);
                _oldCodeBytes.assign(codeBegin, codeEnd);
                if (hasExtraSections)
                {
                    uint32_t extraSectionOffset = (uint32_t)((uint8_t*)fatHeader->GetSect() - (uint8_t*)fatHeader);
                    ByteVector::const_iterator iterStartBytes = originalMethodBytes->begin();
                    
                    iterStartBytes += extraSectionOffset;
                    _exceptionHandlerManipulator = std::make_shared<ExceptionHandlerManipulator>(iterStartBytes);

                    //_exceptionHandlerManipulator = std::make_shared<ExceptionHandlerManipulator>(originalMethodBytes->begin() + extraSectionOffset);
                }
                else
                {
                    _exceptionHandlerManipulator = std::make_shared<ExceptionHandlerManipulator>();
                }

                return;
            }

            if (!tinyHeader->IsTiny())
            {
                LogError(_function->ToString(), L": Is not tiny or fat.");
                throw FunctionManipulatorException();
            }

            // tiny headers need to be converted
            GetHeader()->SetSize(sizeof(COR_ILMETHOD_FAT) / 4);
            // the max stack size of a tiny header is 8, so we default to 8 here
            GetHeader()->SetMaxStack(8);
            GetHeader()->SetFlags(CorILMethod_FatFormat | CorILMethod_InitLocals);
            // tiny headers don't have any local variables so initialize the token to 0 when converting to FAT
            GetHeader()->SetLocalVarSigTok(0);

            uint8_t* codeBegin = tinyHeader->GetCode();
            uint8_t* codeEnd = codeBegin + tinyHeader->GetCodeSize();
            _oldCodeBytes.assign(codeBegin, codeEnd);

            // tiny headers don't have extra sections, create empty ones
            _exceptionHandlerManipulator = std::make_shared<ExceptionHandlerManipulator>();
        }

        // extract the local variables signature from the header
        void ExtractLocalVariablesSignature()
        {
            LogTrace(_function->ToString(), L": Acquiring local variable signature.");

            auto localVariablesSignatureToken = GetHeader()->GetLocalVarSigTok();
            
            // if there are no local variables, create a new signature with a count of 0
            if (localVariablesSignatureToken == 0)
            {
                // see ECMA-335 II.23.2.6
                _newLocalVariablesSignature.clear();
                _newLocalVariablesSignature.push_back(0x7);
                _newLocalVariablesSignature.push_back(0x0);
                return;
            }

            // if there was a signature previously, use it as a base
            _newLocalVariablesSignature.clear();
            auto originalLocalVariablesSignature = _function->GetSignatureFromToken(localVariablesSignatureToken);
            _newLocalVariablesSignature.assign(originalLocalVariablesSignature->begin(), originalLocalVariablesSignature->end());
        }

        COR_ILMETHOD_FAT* GetHeader()
        {
            return (COR_ILMETHOD_FAT*)(_newHeader.data());
        }

        std::function<void()> GetArrayOfTypeParametersLamdba()
        {
            return [&]() {
                // create a Type array big enough to hold all of the method parameters
                uint16_t parameterCount = uint16_t(_methodSignature->_parameters->size());
                _instructions->Append(CEE_LDC_I4, uint32_t(parameterCount));
                _instructions->Append(CEE_NEWARR, _X("[mscorlib]System.Type"));

                // pack the type of each method parameter into our new Type[]
                for (uint16_t i = 0; i < parameterCount; ++i)
                {
                    // get an extra copy of the array (it will be popped off the stack each time we add an element to it)
                    _instructions->Append(CEE_DUP);
                    // the index into the array that we want to set
                    _instructions->Append(CEE_LDC_I4, uint32_t(i));
                    // the type of the method parameter we want to add to the array
                    _instructions->AppendTypeOfArgument(_methodSignature->_parameters->at(i));
                    // write the element to the array (pops the array, the index and the parameter off the stack)
                    _instructions->Append(CEE_STELEM_REF);
                }
            };
        }

        void BuildObjectArrayOfParameters()
        {
            // create an object array big enough to hold all of the method parameters
            uint16_t parameterCount = uint16_t(_methodSignature->_parameters->size());
            _instructions->Append(CEE_LDC_I4, uint32_t(parameterCount));
            _instructions->Append(CEE_NEWARR, _X("[mscorlib]System.Object"));
            // pack all method parameters into our new object[]
            for (uint16_t i = 0; i < parameterCount; ++i)
            {
                // get an extra copy of the array (it will be popped off the stack each time we add an element to it)
                _instructions->Append(CEE_DUP);
                // the index into the array that we want to set
                _instructions->Append(CEE_LDC_I4, uint32_t(i));
                // the method parameter we want to add to the array, boxed if necessary
                _instructions->AppendLoadArgumentAndBox(i + (_methodSignature->_hasThis ? 1 : 0), _methodSignature->_parameters->at(i));
                // write the element to the array (pops the array, the index and the parameter off the stack)
                _instructions->Append(CEE_STELEM_REF);
            }
        }

        void WriteLineToConsole(xstring_t message)
        {
            _instructions->AppendString(message);
            _instructions->Append(CEE_CALL, _X("void [mscorlib]System.Console::WriteLine(string)"));
        }

        // Load the assembly using its full path and then load the given type from the assembly.
        void LoadType(xstring_t assemblyPath, xstring_t typeName)
        {
            _instructions->AppendString(assemblyPath);
            _instructions->Append(CEE_CALL, _X("class [mscorlib]System.Reflection.Assembly [mscorlib]System.Reflection.Assembly::LoadFrom(string)"));
            _instructions->AppendString(typeName);
            _instructions->Append(CEE_CALLVIRT, _X("instance class [mscorlib]System.Type [mscorlib]System.Reflection.Assembly::GetType(string)"));
#ifdef DEBUG
            _instructions->Append(CEE_DUP);
            auto afterMissing = _instructions->AppendJump(CEE_BRTRUE);
            WriteLineToConsole(typeName + _X(" not found"));
            _instructions->AppendLabel(afterMissing);
#endif
        }

        // Call System.Type.GetMethod with the given method name
        void LoadMethodInfoFromType(xstring_t methodName, std::function<void()> argumentTypesLambda)
        {
            _instructions->AppendString(methodName);
            if (argumentTypesLambda == NULL)
            {
                _instructions->Append(CEE_CALLVIRT, _X("instance class [mscorlib]System.Reflection.MethodInfo [mscorlib]System.Type::GetMethod(string)"));
            }
            else
            {
                argumentTypesLambda();
                _instructions->Append(CEE_CALLVIRT, _X("instance class [mscorlib]System.Reflection.MethodInfo [mscorlib]System.Type::GetMethod(string, class [mscorlib]System.Type[])"));
            }
#ifdef DEBUG
            _instructions->Append(CEE_DUP);
            auto afterMissing = _instructions->AppendJump(CEE_BRTRUE);
            WriteLineToConsole(methodName + _X(" not found"));
            _instructions->AppendLabel(afterMissing);
#endif
        }

        // Call MethodBase.Invoke(object, object[])
        void InvokeMethodInfo()
        {
            _instructions->Append(CEE_CALLVIRT, _X("instance object [mscorlib]System.Reflection.MethodBase::Invoke(object, object[])"));
        }

        // Ideally we would have a configuration singleton, but that isn't a thing. 
        static bool IsCachingDisabled()
        {
            return nullptr != std::getenv("NEWRELIC_DISABLE_APPDOMAIN_CACHING");
        }

        // Load the MethodInfo instance for the given class and method onto the stack.
        // The MethodInfo will be cached in the AppDomain to improve performance if useCache is true.
        // The function id is used as a tie-breaker for overloaded methods when computing the key name for the app domain cache.
        void LoadMethodInfo(xstring_t assemblyPath, xstring_t className, xstring_t methodName, uintptr_t functionId, std::function<void()> argumentTypesLambda, bool useCache)
        {
            if (useCache && !IsCachingDisabled())
            {
                auto keyName = className + _X(".") + methodName + _X("_") + to_xstring((unsigned long)functionId);
                _instructions->AppendString(keyName);
                _instructions->AppendString(assemblyPath);
                _instructions->AppendString(className);
                _instructions->AppendString(methodName);
                if (argumentTypesLambda == NULL)
                {
                    _instructions->Append(CEE_LDNULL);
                }
                else
                {
                    argumentTypesLambda();
                }
                
                _instructions->Append(CEE_CALL, _X("class [mscorlib]System.Reflection.MethodInfo [mscorlib]System.CannotUnloadAppDomainException::GetMethodFromAppDomainStorageOrReflectionOrThrow(string,string,string,string,class [mscorlib]System.Type[])"));
            }
            else
            {
                LoadType(assemblyPath, className);
                LoadMethodInfoFromType(methodName, argumentTypesLambda);
            }
        }

        // Creates an array of elementLoadLanbdas.size() and loads the elements into the array
        // by invoking the lambdas.
        void LoadArray(std::list<std::function<void()>> elementLoadLambdas)
        {
            _instructions->Append(CEE_LDC_I4, uint32_t(elementLoadLambdas.size()));
            _instructions->Append(CEE_NEWARR, _X("[mscorlib]System.Object"));
            uint32_t index = 0;

            for (auto func : elementLoadLambdas)
            {
                auto nextIndex = index++;
                // get an extra copy of the array (it will be popped off the stack each time we add an element to it)
                _instructions->Append(CEE_DUP);
                // the index into the array that we want to set
                _instructions->Append(CEE_LDC_I4, nextIndex);

                func();

                // write the element to the array (pops the array, the index and the parameter off the stack)
                _instructions->Append(CEE_STELEM_REF);
            }
        }

        // Writes a try..catch block, invoking the try and catch lambdas to inject the code for those blocks.
        void TryCatch(std::function<void()> tryLambda, std::function<void()> catchLambda)
        {
            // try {
            _instructions->AppendTryStart();

            tryLambda();

            // } // end try
            auto afterCatch = _instructions->AppendJump(CEE_LEAVE);
            _instructions->AppendTryEnd();

            // catch (System.Exception) {
            _instructions->AppendCatchStart();

            catchLambda();

#ifdef TEST_UNSAFE
            _instructions->Append(CEE_RETHROW);
#endif

            _instructions->AppendJump(afterCatch, CEE_LEAVE);

            // } // catch end
            _instructions->AppendCatchEnd();
            _instructions->AppendLabel(afterCatch);
        }

        static void Return(const InstructionSetPtr& instructions, const SignatureParser::ReturnTypePtr& returnType, const uint16_t& resultLocalIndex)
        {
            if (returnType->_kind != SignatureParser::ReturnType::VOID_RETURN_TYPE)
                instructions->AppendLoadLocal(resultLocalIndex);
            instructions->Append(_X("ret"));
        }

        static void ThrowException(const InstructionSetPtr& instructions, const xstring_t& message, const bool& inMscorlib = false)
        {
            auto exception = inMscorlib ? _X("instance void System.Exception::.ctor(string)") : _X("instance void [[mscorlib]]System.Exception::.ctor(string)");

            instructions->AppendString(message);
            instructions->Append(CEE_NEWOBJ, exception);
            instructions->Append(CEE_THROW);
        }

        static void ThrowExceptionIfStackItemIsNull(const InstructionSetPtr& instructions, const xstring_t& message, const bool& inMscorlib = false)
        {
            instructions->Append(CEE_DUP);
            auto afterThrow = instructions->AppendJump(CEE_BRTRUE);
            ThrowException(instructions, message, inMscorlib);
            instructions->AppendLabel(afterThrow);
        }

        void WriteLocalsToHeader()
        {
            LogTrace(_function->ToString(), L": Writing locals to header.");
            auto localSignaturesToken = _function->GetTokenFromSignature(_newLocalVariablesSignature);
            GetHeader()->SetLocalVarSigTok(localSignaturesToken);
        }

        // append the return type of this function to the locals signature and return the index to it
        static uint16_t AppendReturnTypeLocal(ByteVector& localsSignature, SignatureParser::MethodSignaturePtr signature)
        {
            return AppendToLocalsSignature(*signature->_returnType->ToBytes(), localsSignature);
        }

        // appends the given token to the new locals signature and returns the index to it
        static uint16_t AppendToLocalsSignature(xstring_t typeString, sicily::codegen::ITokenizerPtr tokenizer, ByteVector& localsSignature)
        {
            // turn the type string into a type token
            auto typeBytes = TypeStringToToken(typeString, tokenizer);
            return AppendToLocalsSignature(typeBytes, localsSignature);
        }

        // append some bytes representing a type to the locals signature and return the index to it
        static uint16_t AppendToLocalsSignature(const ByteVector& typeBytes, ByteVector& localsSignature)
        {
            // append the bytes
            localsSignature.insert(localsSignature.end(), typeBytes.begin(), typeBytes.end());
            // increment the local counter
            auto newLocalIndex = IncrementLocalCount(localsSignature);
            // return the local index to the new local
            return newLocalIndex;
        }

        static uint16_t IncrementLocalCount(ByteVector& localsSignature)
        {
            // uncompress the local variable count and get an iterator that points to the next byte after the count
            ByteVector::const_iterator iterator = localsSignature.begin() + 1;
            //CorSigUncompressData advances 'iterator'
            auto localCount = sicily::codegen::ByteCodeGenerator::CorSigUncompressData(iterator, localsSignature.end());
            if (localCount >= 0xfffe)
            {
                LogError("Extracted local count (", std::hex, std::showbase, localCount, ") is too big to add locals to (>= 0xfffe)", std::resetiosflags(std::ios_base::basefield|std::ios_base::showbase));
                throw FunctionManipulatorException();
            }
            // increment and compress the count
            auto compressedCount = sicily::codegen::ByteCodeGenerator::CorSigCompressData(++localCount);
            // erase the old count from the vector
            localsSignature.erase(localsSignature.begin() + 1, iterator);
            // insert the new count into the vector at the appropriate location
            localsSignature.insert(localsSignature.begin() + 1, compressedCount.begin(), compressedCount.end());

            return uint16_t(localCount - 1);
        }

        static ByteVector TypeStringToToken(xstring_t typeString, sicily::codegen::ITokenizerPtr tokenizer)
        {
            sicily::Scanner scanner(typeString);
            sicily::Parser parser;
            auto type = parser.Parse(scanner);
            sicily::codegen::ByteCodeGenerator bytecodeGenerator(tokenizer);
            return bytecodeGenerator.TypeToBytes(type);
        }

        ByteVector CombineHeaderInstructionsAndExtraSections()
        {
            LogTrace(_function->ToString(), L": Building the byte array for this method.");
            ByteVector combined;
            AppendHeaderBytes(combined);
            AppendInstructionBytes(combined);
            AppendExtraSectionBytes(combined);
            return combined;
        }

        void AppendHeaderBytes(ByteVector& bytes)
        {
            LogTrace(_function->ToString(), L": Appending the header to the method byte array.");
            bytes.insert(bytes.end(), _newHeader.begin(), _newHeader.end());
        }

        void AppendInstructionBytes(ByteVector& bytes)
        {
            LogTrace(_function->ToString(), L": Appending the bytecode to the method byte array.");
            auto instructionBytes = _instructions->GetBytes();
            bytes.insert(bytes.end(), instructionBytes.begin(), instructionBytes.end());
        }

        void AppendExtraSectionBytes(ByteVector& bytes)
        {
            LogTrace(_function->ToString(), L": Appending extra sections to the method byte array.");
            // pad to the next 4-byte boundary
            while (bytes.size() & 0x3)
            {
                bytes.push_back(0x00);
            }
            // get the bytes for the extra section
            auto extraSectionBytes = _exceptionHandlerManipulator->GetExtraSectionBytes(_instructions->GetUserCodeOffset());
            // and append them to the method bytes
            bytes.insert(bytes.end(), extraSectionBytes->begin(), extraSectionBytes->end());
        }

        bool HasSignature(const ByteVector& signature)
        {
            if (*_function->GetSignature() == signature) return true;
            return false;
        }
    };
}}}
