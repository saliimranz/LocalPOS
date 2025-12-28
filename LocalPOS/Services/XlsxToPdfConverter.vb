Imports System
Imports System.Diagnostics
Imports System.IO
Imports System.Text.RegularExpressions

Public NotInheritable Class XlsxToPdfConverter
    Private Sub New()
    End Sub

    Public Shared Function Convert(xlsxBytes As Byte(), Optional timeoutMs As Integer = 60000) As Byte()
        If xlsxBytes Is Nothing OrElse xlsxBytes.Length = 0 Then
            Throw New ArgumentException("XLSX content is empty.", NameOf(xlsxBytes))
        End If

        Dim workDir = Path.Combine(Path.GetTempPath(), "localpos_invoice_pdf_" & Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(workDir)

        Dim inputPath = Path.Combine(workDir, "invoice.xlsx")
        File.WriteAllBytes(inputPath, xlsxBytes)

        Dim startInfo As New ProcessStartInfo() With {
            .FileName = "soffice",
            .Arguments = $"--headless --nologo --nolockcheck --nodefault --norestore --convert-to pdf --outdir ""{workDir}"" ""{inputPath}""",
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .WorkingDirectory = workDir
        }

        Dim stdout As String = ""
        Dim stderr As String = ""
        Try
            Using p = Process.Start(startInfo)
                If p Is Nothing Then
                    Throw New InvalidOperationException("Unable to start PDF conversion process.")
                End If

                If Not p.WaitForExit(Math.Max(1000, timeoutMs)) Then
                    Try
                        p.Kill()
                    Catch
                        ' Ignore.
                    End Try
                    Throw New TimeoutException("PDF conversion timed out.")
                End If

                stdout = p.StandardOutput.ReadToEnd()
                stderr = p.StandardError.ReadToEnd()

                If p.ExitCode <> 0 Then
                    Throw New InvalidOperationException($"PDF conversion failed (exit {p.ExitCode}). {SafeProcessMessage(stdout, stderr)}")
                End If
            End Using

            ' LibreOffice uses the base name of the input file for output.
            Dim pdfPath = Path.Combine(workDir, "invoice.pdf")
            If Not File.Exists(pdfPath) Then
                Dim baseNamePdf = Path.Combine(workDir, Path.GetFileNameWithoutExtension(inputPath) & ".pdf")
                If File.Exists(baseNamePdf) Then
                    pdfPath = baseNamePdf
                End If
            End If

            If Not File.Exists(pdfPath) Then
                ' Be defensive in case the output name changes.
                Dim candidates = Directory.GetFiles(workDir, "*.pdf")
                If candidates IsNot Nothing AndAlso candidates.Length > 0 Then
                    pdfPath = candidates(0)
                End If
            End If

            If Not File.Exists(pdfPath) Then
                Throw New FileNotFoundException($"PDF conversion did not produce output. {SafeProcessMessage(stdout, stderr)}")
            End If

            Return File.ReadAllBytes(pdfPath)
        Finally
            Try
                Directory.Delete(workDir, True)
            Catch
                ' Best effort cleanup.
            End Try
        End Try
    End Function

    Private Shared Function SafeProcessMessage(stdout As String, stderr As String) As String
        Dim raw = (If(stdout, String.Empty) & " " & If(stderr, String.Empty)).Trim()
        If String.IsNullOrWhiteSpace(raw) Then
            Return String.Empty
        End If
        raw = Regex.Replace(raw, "\s+", " ")
        If raw.Length > 400 Then
            raw = raw.Substring(0, 400) & "..."
        End If
        Return raw
    End Function
End Class

