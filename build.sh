#!/bin/bash

output_directory="bin/dist"
project="SphereServerScriptAnalyser.csproj"

echo "================================"
echo ".NET 9.0 Build Script"
echo "================================"
echo ""

if [ ! -f "$project" ]; then
    echo "[HATA] Proje dosyasi bulunamadi: $project"
    read -p "Kapatmak icin Enter'a basin..."
    exit 1
fi

echo "[1/3] Temizleniyor..."
if [ -d "$output_directory" ]; then
    rm -rf "$output_directory"
    echo "  -> $output_directory temizlendi"
else
    echo "  -> Temizlenecek dosya yok"
fi

echo ""
echo "[2/3] Derleniyor (Release - Single EXE)..."
echo "  -> Platform: win-x64"
echo "  -> Mod: Release"
echo ""

dotnet publish "$project" \
    -c Release \
    -r win-x64 \
    -p:EnableCompressionInSingleFile=true \
    -o "$output_directory"


exit_code=$?

echo ""
echo "================================"
if [ $exit_code -eq 0 ]; then
    echo "[3/3] BASARILI!"
    echo "================================"
    echo "Cikti konumu: $output_directory"
    echo ""
    if [ -d "$output_directory" ]; then
        echo "Olusturulan dosyalar:"
        ls -lh "$output_directory"
    fi
else
    echo "[!] HATA: Derleme basarisiz oldu!"
    echo "Hata kodu: $exit_code"
    echo "================================"
fi

echo ""
echo "Script tamamlandi. Kapatmak icin Enter'a basin..."
read