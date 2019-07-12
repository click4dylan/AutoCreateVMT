Imports System.Threading.Thread
Imports System.Windows.Forms

Module Module1
    Private Enum BumpMapType As Byte
        NONE = 0
        BUMPMAP = 1
        BUMPMAP_SPECULAR = 2
    End Enum
    Private Function GetBumpMapType(ByVal request As Byte) As String
        Select Case request
            Case BumpMapType.NONE
                Return "None"
            Case BumpMapType.BUMPMAP
                Return "Bumpmap"
            Case BumpMapType.BUMPMAP_SPECULAR
                Return "Bumpmap with Specular Mask"
        End Select
        Return "Unknown"
    End Function
    Sub Main()
        Console.Title = "Automatically Generate VMT Files v" & FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion
        Dim dir = IO.Directory.GetCurrentDirectory & "\"
        Dim listoftextureswithoutvmt As New Dictionary(Of String, Byte)
        Dim listoflonebumpmaps As New List(Of String)
        Dim files = IO.Directory.GetFiles(dir)


        'Get the list of textures that aren't bump maps and need a .VMT. If a bump map exists for them, we will include it
        For Each file In files
            If Not file.ToLower.EndsWith("_nrm.vtf") And Not file.ToLower.EndsWith("_nrmspec.vtf") And file.ToLower.EndsWith(".vtf") Then
                Dim explode = file.Split("\")
                Dim texturename = explode(explode.Length - 1)
                texturename = texturename.Substring(0, texturename.Length - 4)
                If Not IO.File.Exists(dir & texturename & ".vmt") Then
                    Dim bumpmap = BumpMapType.NONE
                    If IO.File.Exists(dir & texturename & "_nrmspec.vtf") Then
                        bumpmap = BumpMapType.BUMPMAP_SPECULAR
                    ElseIf IO.File.Exists(dir & texturename & "_nrm.vtf") Then
                        bumpmap = BumpMapType.BUMPMAP
                    End If
                    listoftextureswithoutvmt.Add(texturename, bumpmap)
                End If
            End If
            Sleep(1)
        Next

        'Create .VMT file for all textures that don't have one, include any bump maps if needed
        If listoftextureswithoutvmt.Count > 0 Then
            For Each texture In listoftextureswithoutvmt
                Console.WriteLine("Creating vmt for: " & texture.Key & " (bumpmap type: " & GetBumpMapType(texture.Value) & ")")
                CreateVMT(dir, texture.Key, texture.Value)
                Sleep(1)
            Next
        Else
            Console.WriteLine("No basetexture .vtf files found requiring a .vmt in the current directory.")
        End If

        'Get the list of lone bump maps and add them to their parent .VMT
        For Each file In files
            'These two if statements could be combined but to keep things simple we'll separate them
RestartParseVMT_Bump:
            If file.ToLower.EndsWith("_nrm.vtf") Then
                'Regular bump map
                Dim explode = file.Split("\")
                Dim texturename = explode(explode.Length - 1)
                texturename = texturename.Substring(0, texturename.Length - 8)
                If IO.File.Exists(dir & texturename & ".vmt") Then
                    Dim bumpmapexists As Boolean = False
                    Dim vmtdata = ReadVMT(dir, texturename)
                    If vmtdata.Count > 0 Then
                        For Each line In vmtdata
                            If line.Contains("bumpmap") Then bumpmapexists = True
                            Sleep(1)
                        Next
                        If Not bumpmapexists Then
                            listoflonebumpmaps.Add(texturename & "_nrm")
                            Dim explode2 = dir.Split("\")
                            Dim root = explode2(explode.Length - 2)
                            'Replace } with the bumpmap, then add } back
                            vmtdata(vmtdata.Count - 1) = ControlChars.Tab & Quote & "$bumpmap" & Quote & " " & Quote & root & "/" & texturename & "_nrm" & Quote
                            vmtdata.Add("}")
                            Console.WriteLine("Adding missing bumpmap for: " & texturename & ".vmt")
                            UpdateVMT(dir, texturename, vmtdata)
                            GoTo RestartParseVMT_Bump
                        End If
                    Else
                        IO.File.Delete(dir & texturename & ".vmt")
                        Threading.Thread.Sleep(100)
                        CreateVMT(dir, texturename, BumpMapType.BUMPMAP)
                        Console.WriteLine("Creating new .vmt for empty file: " & dir & texturename & ".vmt")
                    End If
                End If

            ElseIf file.ToLower.EndsWith("_nrmspec.vtf") Then
                'Bump map with specular embedded
                Dim explode = file.Split("\")
                Dim texturename = explode(explode.Length - 1)
                texturename = texturename.Substring(0, texturename.Length - 12)
RestartParseVMT_Spec:
                If IO.File.Exists(dir & texturename & ".vmt") Then
                    Dim bumpmapexists As Boolean = False
                    Dim specularmapset As Boolean = False
                    Dim vmtdata = ReadVMT(dir, texturename)
                    If vmtdata.Count > 0 Then
                        For Each line In vmtdata
                            If line.Contains("bumpmap") Then bumpmapexists = True
                            If line.Contains("normalmapalphaenvmapmask") Then specularmapset = True
                            Sleep(1)
                        Next
                        If Not bumpmapexists Or Not specularmapset Then
                            listoflonebumpmaps.Add(texturename & "_nrmspec")
                            Dim explode2 = dir.Split("\")
                            Dim root = explode2(explode.Length - 2)
                            'Replace } with the bumpmap, then add } back
                            vmtdata(vmtdata.Count - 1) = ControlChars.Tab & Quote & "$bumpmap" & Quote & " " & Quote & root & "/" & texturename & "_nrmspec" & Quote
                            If Not specularmapset Then
                                vmtdata.Add(ControlChars.Tab & Quote & "$normalmapalphaenvmapmask" & Quote & " " & Quote & "1" & Quote)
                            End If
                            vmtdata.Add("}")
                            Console.WriteLine("Adding missing bumpmap with specular map for: " & texturename & ".vmt")
                            UpdateVMT(dir, texturename, vmtdata)
                        End If
                    Else
                        IO.File.Delete(dir & texturename & ".vmt")
                        Threading.Thread.Sleep(100)
                        CreateVMT(dir, texturename, BumpMapType.BUMPMAP_SPECULAR)
                        Console.WriteLine("Creating new .vmt for empty file: " & dir & texturename & ".vmt")
                        GoTo RestartParseVMT_Spec
                    End If
                End If
            End If
            Sleep(1)
        Next

        If listoflonebumpmaps.Count = 0 Then
            Console.WriteLine("No lone bumpmaps were found needing to be added to a .vmt")
        End If

        Console.WriteLine(ControlChars.NewLine & "Press any key to exit.")
        Console.Read()
    End Sub
    Private Const Quote = ControlChars.Quote
    Function ReadVMT(ByVal dir As String, ByVal file As String) As List(Of String)
        Dim reader As New IO.StreamReader(dir & file & ".vmt")
        Dim contents As New List(Of String)
        While reader.Peek > 0
            contents.Add(reader.ReadLine)
        End While
        reader.Close()
        Return contents
    End Function
    Sub UpdateVMT(ByVal dir As String, ByVal file As String, ByVal data As List(Of String))
        IO.File.Delete(dir & file & ".vmt")
        Dim explode = dir.Split("\")
        Dim root = explode(explode.Length - 2)
        Dim writer As New IO.StreamWriter(dir & file & ".vmt", False)
        For Each line In data
            writer.WriteLine(line)
        Next
        writer.Flush()
        writer.Close()
    End Sub
    Sub CreateVMT(ByVal dir As String, ByVal file As String, ByVal bumpmap As Byte)
        Dim explode = dir.Split("\")
        Dim root As String = ""
        Dim folders As New List(Of String)
        For i As Integer = 1 To explode.Length - 1
            root = explode(explode.Length - i)
            If Not root.Length = 0 Then
                If root = "materials" Then
                    root = explode(explode.Length - (i - 1))
                    Exit For
                Else
                    folders.Add(root)
                End If
            End If
        Next
        For Each nameoffolder In folders
            If Not folders.IndexOf(nameoffolder) = folders.Count - 1 Then
                root = root & "\" & nameoffolder
            End If
        Next
        If root = "" Then
            Console.Write("COULD NOT FIND MATERIALS FOLDER! Place this exe nested inside a materials folder")
            Threading.Thread.Sleep(3000)
            Throw New Exception("COULD NOT FIND MATERIALS FOLDER! Place this exe nested inside a materials folder")
        End If
        Dim writer As New IO.StreamWriter(dir & file & ".vmt", False)
        Dim filename = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.ToLower
        If filename.Contains("mdl") Then
            'If our file name contains MDL, create a vmt for models
            writer.WriteLine(Quote & "VertexLitGeneric" & Quote)
        ElseIf filename.Contains("spr") Then
            writer.WriteLine(Quote & "UnlitGeneric" & Quote)
        Else
            'Otherwise, create a vmt for regular urfaces
            writer.WriteLine(Quote & "LightmappedGeneric" & Quote)
        End If

        writer.WriteLine("{")
        writer.WriteLine(ControlChars.Tab & Quote & "$basetexture" & Quote & " " & Quote & root & "/" & file & Quote)
        If filename.Contains("spr") Then
            writer.WriteLine(ControlChars.Tab & Quote & "$translucent" & Quote & " " & Quote & "1" & Quote)
            writer.WriteLine(ControlChars.Tab & Quote & "$ignorez" & Quote & " " & Quote & "1" & Quote)
            writer.WriteLine(ControlChars.Tab & Quote & "$vertexcolor" & Quote & " " & Quote & "1" & Quote)
            writer.WriteLine(ControlChars.Tab & Quote & "$vertexalpha" & Quote & " " & Quote & "1" & Quote)
        End If
        If bumpmap = BumpMapType.BUMPMAP Then
            writer.WriteLine(ControlChars.Tab & Quote & "$bumpmap" & Quote & " " & Quote & root & "/" & file & "_nrm" & Quote)
        ElseIf bumpmap = BumpMapType.BUMPMAP_SPECULAR Then
            writer.WriteLine(ControlChars.Tab & Quote & "$bumpmap" & Quote & " " & Quote & root & "/" & file & "_nrmspec" & Quote)
            writer.WriteLine(ControlChars.Tab & Quote & "$normalmapalphaenvmapmask" & Quote & " " & Quote & "1" & Quote)
            writer.WriteLine(ControlChars.Tab & Quote & "$envmap " & Quote & "env_cubemap" & Quote)
        End If
        writer.WriteLine("}")
        writer.Flush()
        writer.Close()
    End Sub
End Module
