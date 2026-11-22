"""Lossless PSD packaging: preserve generated PNG channel values and full-canvas coordinates."""
import struct,zlib
from pathlib import Path
root=Path(__file__).parent/'Live2D'
def png(path):
    data=path.read_bytes(); assert data[:8]==b'\x89PNG\r\n\x1a\n'
    pos=8; compressed=b''
    while pos<len(data):
        length=struct.unpack('>I',data[pos:pos+4])[0]; kind=data[pos+4:pos+8]; body=data[pos+8:pos+8+length];pos+=12+length
        if kind==b'IHDR': w,h,depth,color,comp,filt,inter=struct.unpack('>IIBBBBB',body)
        if kind==b'IDAT':compressed+=body
    assert depth==8 and color==6 and inter==0,(depth,color,inter)
    scan=zlib.decompress(compressed);stride=w*4;rows=[];prev=bytearray(stride);pos=0
    for y in range(h):
        f=scan[pos];row=bytearray(scan[pos+1:pos+1+stride]);pos+=stride+1
        for i in range(stride):
            a=row[i-4] if i>=4 else 0;b=prev[i];c=prev[i-4] if i>=4 else 0
            if f==1:row[i]=(row[i]+a)&255
            elif f==2:row[i]=(row[i]+b)&255
            elif f==3:row[i]=(row[i]+((a+b)//2))&255
            elif f==4:
                p=a+b-c;pa=abs(p-a);pb=abs(p-b);pc=abs(p-c)
                row[i]=(row[i]+(a if pa<=pb and pa<=pc else b if pb<=pc else c))&255
            else:assert f==0
        rows.append(row);prev=row
    pixels=b''.join(rows)
    return w,h,[pixels[c::4] for c in range(4)]
u16=lambda n:struct.pack('>H',n)
u32=lambda n:struct.pack('>I',n)
layers=[]
for name,hidden in [('EyesClosed',True),('EyesOpen',False),('BodyBase',False)]:
    w,h,channels=png(root/'Source'/(name+'.png'))
    assert (w,h)==(1024,1536)
    encoded=[];records=b''
    for ch in [-1,0,1,2]:
        raw=channels[3 if ch==-1 else ch];packed=u16(0)+raw
        records+=struct.pack('>hI',ch,len(packed));encoded.append(packed)
    label=bytes([len(name)])+name.encode();label+=b'\0'*((-len(label))%4)
    extra=u32(0)+u32(0)+label
    record=struct.pack('>iiiiH',0,0,h,w,4)+records+b'8BIMnorm'+bytes([255,0,2 if hidden else 0,0])+u32(len(extra))+extra
    layers.append((record,b''.join(encoded)))
info=struct.pack('>h',-len(layers))+b''.join(x[0] for x in layers)+b''.join(x[1] for x in layers)
info+=b'\0'*(len(info)%2)
mask=u32(len(info))+info+u32(0)
w,h,merged=png(root/'Source'/'Character.png')
resolution=struct.pack('>IHHIHH',72*65536,1,1,72*65536,1,1)
resources=b'8BIM'+u16(1005)+b'\0\0'+u32(len(resolution))+resolution
psd=b'8BPS'+u16(1)+b'\0'*6+u16(4)+u32(h)+u32(w)+u16(8)+u16(3)+u32(0)+u32(len(resources))+resources+u32(len(mask))+mask+u16(0)+b''.join(merged)
out=root/'LeafAndLeap-Cel-v2.psd';out.write_bytes(psd)
print(out, len(psd), '3 aligned layers; EyesClosed hidden')
