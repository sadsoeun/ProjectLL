const fs=require('fs');
const {pathToFileURL}=require('url');
const {chromium}=require('C:/Program Files/WindowsApps/OpenAI.CodexPrimaryRuntime.v26-1112-611-0_26.1112.611.0_x64__3k8sg7r9htsxt/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
 const page=await browser.newPage({viewport:{width:800,height:1000},deviceScaleFactor:2});
 const source='C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42/conversation-export/게임제작_대화내역.html';
 await page.goto(pathToFileURL(source).href,{waitUntil:'load'});
 await page.evaluate(()=>document.fonts.ready);
 await page.evaluate(async()=>{const imgs=[...document.images];imgs.forEach(i=>i.loading='eager');await Promise.all(imgs.map(i=>i.decode().catch(()=>{})));});
 await page.locator('#m21').scrollIntoViewIfNeeded();
 const a=await page.locator('#m21').boundingBox(),b=await page.locator('#m22').boundingBox();
 const clip={x:a.x,y:a.y,width:a.width,height:b.y+b.height-a.y};
 await page.screenshot({path:__dirname+'/Captures/ui-conversation-excerpt.png',clip});
 const messages=await page.locator('#m21, #m22').allTextContents();
 fs.writeFileSync(__dirname+'/Captures/ui-conversation-source.json',JSON.stringify({source,messages,clip,description:'Existing conversation archive, consecutive messages m21 and m22; no text or styling changes.'},null,2));
 await browser.close();
 console.log(JSON.stringify({file:'ui-conversation-excerpt.png',clip}));
})();
