$flatc = $env:FLATC_PATH
if (-not $flatc) { $flatc = "flatc" }
& $flatc --csharp -o Generated Protocols/*.fbs
