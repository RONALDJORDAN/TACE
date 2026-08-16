import sys
from PIL import Image

def main():
    if len(sys.argv) < 3:
        print("Usage: python convert.py <input> <output>")
        sys.exit(1)
    
    img = Image.open(sys.argv[1])
    img.save(sys.argv[2], format='ICO', sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print(f"Saved {sys.argv[2]}")

if __name__ == '__main__':
    main()
