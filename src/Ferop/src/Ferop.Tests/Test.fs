module Ferop.Tests

open System
open NUnit.Framework
open System.Runtime.InteropServices

open Ferop

#nowarn "51"

type [<Struct>] RecursiveStruct50 =
    val X : int

type [<Struct>] RecursiveStruct49 =
    val X : int
    val Y : RecursiveStruct50

type [<Struct>] RecursiveStruct48 =
    val X : int
    val Y : RecursiveStruct49

type [<Struct>] RecursiveStruct47 =
    val X : int
    val Y : RecursiveStruct48

type [<Struct>] RecursiveStruct46 =
    val X : int
    val Y : RecursiveStruct47

type [<Struct>] RecursiveStruct45 =
    val X : int
    val Y : RecursiveStruct46

type [<Struct>] RecursiveStruct44 =
    val X : int
    val Y : RecursiveStruct45

type [<Struct>] RecursiveStruct43 =
    val X : int
    val Y : RecursiveStruct44

type [<Struct>] RecursiveStruct42 =
    val X : int
    val Y : RecursiveStruct43

type [<Struct>] RecursiveStruct41 =
    val X : int
    val Y : RecursiveStruct42

type [<Struct>] RecursiveStruct40 =
    val X : int
    val Y : RecursiveStruct41

[<Struct>]
type Struct2 =
    val X : Struct3
    val Y : int

and [<Struct>]
    Struct1 =
    val X : single
    val Y : single
    val Z : single
    val W : nativeptr<single>
    val St2 : Struct2
    val St3 : Struct3

and [<Struct>]
    Struct3 =
        val X : double
        val Y : double

        new (x, y) = { X = x; Y = y }

[<Struct>]
type Struct4 =
    val X : single
    val Y : single

type Enum1 =
    | Sub1 = 0
    | Sub2 = 1

type Enum2 =
    | Sub1 = 3
    | Sub2 = 1

[<Struct>]
type StructWithEnums =
    val Value : Enum1

[<Ferop>]
[<Header ("""
#include <stdio.h>
""")>]
[<Source ("""
int _globalX = 500;
""")>]
type Tests =
    [<Import; MI (MIO.NoInlining)>]
    static member private testByte_private (x: byte) : byte = C """ return x; """

    static member testByte (x: byte) : byte = Tests.testByte_private x

    [<Import; MI (MIO.NoInlining)>]
    static member testTwoBytes (x: byte) (y: byte) : byte = C """ return x + y; """

    [<Import; MI (MIO.NoInlining)>]
    static member testSByte (x: sbyte) : sbyte = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testUInt16 (x: uint16) : uint16 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testInt16 (x: int16) : int16 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testUInt32 (x: uint32) : uint32 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testInt32 (x: int) : int = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testUInt64 (x: uint64) : uint64 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testInt64 (x: int64) : int64 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testSingle (x: single) : single = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testDouble (x: double) : double = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testStruct1 (x: Struct1) : Struct1 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testStruct2 (x: Struct2) : Struct2 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testStruct3 (x: Struct3) : Struct3 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    static member testStruct3Value (x: Struct3) : float = C """ return x.Y; """

    [<Export>]
    static member exported_testByte (x: byte) : byte = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testByte (x: byte) : byte = C """ return Tests_exported_testByte (x); """

    [<Export>]
    static member exported_testTwoBytes (x: byte) (y: byte) : byte = x + y

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testTwoBytes (x: byte) (y: byte) : byte = C """ return Tests_exported_testTwoBytes (x, y); """

    [<Export>]
    static member exported_testSByte (x: sbyte) : sbyte = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testSByte (x: sbyte) : sbyte = C """ return Tests_exported_testSByte (x); """

    [<Export>]
    static member exported_testUInt16 (x: uint16) : uint16 = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testUInt16 (x: uint16) : uint16 = C """ return Tests_exported_testUInt16 (x); """

    [<Export>]
    static member exported_testInt16 (x: int16) : int16 = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testInt16 (x: int16) : int16 = C """ return Tests_exported_testInt16 (x); """

    [<Export>]
    static member exported_testUInt32 (x: uint32) : uint32 = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testUInt32 (x: uint32) : uint32 = C """ return Tests_exported_testUInt32 (x); """

    [<Export>]
    static member exported_testInt32 (x: int) : int = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testInt32 (x: int) : int = C """ return Tests_exported_testInt32 (x); """

    [<Export>]
    static member exported_testUInt64 (x: uint64) : uint64 = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testUInt64 (x: uint64) : uint64 = C """ return Tests_exported_testUInt64 (x); """

    [<Export>]
    static member exported_testInt64 (x: int64) : int64 = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testInt64 (x: int64) : int64 = C """ return Tests_exported_testInt64 (x); """

    [<Export>]
    static member exported_testSingle (x: single) : single = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testSingle (x: single) : single = C """ return Tests_exported_testSingle (x); """

    [<Export>]
    static member exported_testDouble (x: double) : double = x

    [<Import; MI (MIO.NoInlining)>]
    static member testExported_testDouble (x: double) : double = C """ return Tests_exported_testDouble (x); """

[<Ferop>]
module Tests2 =
    [<Import; MI (MIO.NoInlining)>]
    let testByte (x: byte) : byte = C """ return x; """

[<Ferop>]
module Tests3 =
    [<Import; MI (MIO.NoInlining)>]
    let testEnum1 (x: Enum1) : Enum1 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    let testEnum2 (x: Enum2) : Enum2 = C """ return x; """

    [<Import; MI (MIO.NoInlining)>]
    let testStructWithEnums (x: StructWithEnums) : StructWithEnums = C """ return x; """

[<Ferop>]
module Tests4 =
    [<UnmanagedFunctionPointerAttribute (CallingConvention.Cdecl)>]
    type TestDelegate = delegate of int -> int

    [<Import; MI (MIO.NoInlining)>]
    let testByteArray (x: byte[]) : byte = C """ return x[0]; """

    [<Import; MI (MIO.NoInlining)>]
    let testDelegate (f: TestDelegate) : int = C """ return f (1234); """

    [<Import; MI (MIO.NoInlining)>]
    let testRecursiveStruct (x: RecursiveStruct40) : unit = C """ """

    [<Import; MI (MIO.NoInlining)>]
    let testStructPointer (xp: nativeptr<RecursiveStruct40>) : unit = C """ """

    [<Import; MI (MIO.NoInlining)>]
    let testByRef (xbr: double byref) : unit = C """ *xbr = 30.2; """

    [<Import; MI (MIO.NoInlining)>]
    let testPointer (xp: nativeptr<double>) : unit = C """ *xp = 36.2; """

    [<Import; MI (MIO.NoInlining)>]
    let testArray (xs: double []) : unit = C """ xs[2] = 20.45; """

    [<Import; MI (MIO.NoInlining)>]
    let testStructPointer2 (xp: nativeptr<Struct4>) : unit = C """ """

    [<Import; MI (MIO.NoInlining)>]
    let testNativeInt (x: nativeint) : unit = C """ """

    [<Import; MI (MIO.NoInlining)>]
    let testUnativeInt (x: unativeint) : unit = C """ """

[<Ferop>]
[<Cpp>]
[<Header ("""
#include <iostream>
using namespace std;
""")>]
module TestsCpp =
    [<Import; MI (MIO.NoInlining)>]
    let testCppHelloWorld () : unit = C """cout << "Hello World!\n";"""

[<Test>]
let ``with max value of byte, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testByte (Byte.MaxValue), Byte.MaxValue)

[<Test>]
let ``with max value of sbyte, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testSByte (SByte.MaxValue), SByte.MaxValue)

[<Test>]
let ``with max value of uint16, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testUInt16 (UInt16.MaxValue), UInt16.MaxValue)

[<Test>]
let ``with max value of int16, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testInt16 (Int16.MaxValue), Int16.MaxValue)

[<Test>]
let ``with max value of uint32, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testUInt32 (UInt32.MaxValue), UInt32.MaxValue)

[<Test>]
let ``with max value of int, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testInt32 (Int32.MaxValue), Int32.MaxValue)

[<Test>]
let ``with max value of uint64, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testUInt64 (UInt64.MaxValue), UInt64.MaxValue)

[<Test>]
let ``with max value of int64, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testInt64 (Int64.MaxValue), Int64.MaxValue)

[<Test>]
let ``with max value of single, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testSingle (Single.MaxValue), Single.MaxValue)

[<Test>]
let ``with max value of double, should pass and return the same value`` () =
    Assert.AreEqual (Tests.testDouble (Double.MaxValue), Double.MaxValue)

[<Test>]
let ``with max value of byte, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testByte (Byte.MaxValue), Byte.MaxValue)

[<Test>]
let ``with max value of sbyte, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testSByte (SByte.MaxValue), SByte.MaxValue)

[<Test>]
let ``with max value of uint16, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testUInt16 (UInt16.MaxValue), UInt16.MaxValue)

[<Test>]
let ``with max value of int16, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testInt16 (Int16.MaxValue), Int16.MaxValue)

[<Test>]
let ``with max value of uint32, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testUInt32 (UInt32.MaxValue), UInt32.MaxValue)

[<Test>]
let ``with max value of int, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testInt32 (Int32.MaxValue), Int32.MaxValue)

[<Test>]
let ``with max value of uint64, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testUInt64 (UInt64.MaxValue), UInt64.MaxValue)

[<Test>]
let ``with max value of int64, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testInt64 (Int64.MaxValue), Int64.MaxValue)

[<Test>]
let ``with max value of single, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testSingle (Single.MaxValue), Single.MaxValue)

[<Test>]
let ``with max value of double, should pass and return the same value from exported`` () =
    GC.Collect (2)
    Assert.AreEqual (Tests.testExported_testDouble (Double.MaxValue), Double.MaxValue)

[<Test>]
let ``with a Y value of Struct3, should pass Struct3 and return the correct Y value`` () =
    Assert.AreEqual (Tests.testStruct3Value (Struct3 (5., 53.)), 53.)

[<Test>]
let ``with byte array, should pass byte array and return first element`` () =
    Assert.AreEqual (Tests4.testByteArray ([|255uy|]), 255uy)

[<Test>]
let ``with a delegate type, should pass a delegate and return the result`` () =
    Assert.AreEqual (Tests4.testDelegate (Tests4.TestDelegate (fun x -> x)), 1234)

[<Test>]
let ``with a byref type, should pass a reference to get a new value`` () =
    let mutable x = 1.
    Tests4.testByRef (&x)
    Assert.AreEqual (x, 30.2)

[<Test>]
let ``with a pointer type, should pass a pointer to get a new value`` () =
    let mutable x = 1.
    Tests4.testPointer (&&x)
    Assert.AreEqual (x, 36.2)

[<Test>]
let ``with an array type, should pass an array and have it be modified in the unmanaged world`` () =
    let arr = [|0.;0.;0.;0.;0.;0.|]
    Tests4.testArray (arr)
    Assert.AreEqual (arr.[2], 20.45)

[<Test>]
let ``with a C exported function with cpp, should pass without error`` () =
    TestsCpp.testCppHelloWorld ()
