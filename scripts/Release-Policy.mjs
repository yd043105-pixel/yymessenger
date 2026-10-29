import {readFileSync} from 'node:fs';
import {pathToFileURL} from 'node:url';

export function releaseMode(policy,version,initialRelease){
 if(policy?.schema!==1||policy.platform!=='windows'||typeof policy.firstReleaseApproved!=='boolean'||typeof policy.automaticPublicReleasesAfterLaunch!=='boolean')throw Error('Invalid maintenance release policy');
 if(!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(version))throw Error('Invalid release version');
 if(!policy.firstReleaseApproved||!policy.automaticPublicReleasesAfterLaunch||version==='1.0.0'||version.includes('-'))return 'draft';
 const parts=version.split('.').map(Number);
 if(parts[0]<1)return 'draft';
 return initialRelease?.tagName==='v1.0.0'&&initialRelease.isDraft===false&&initialRelease.isPrerelease===false&&typeof initialRelease.publishedAt==='string'&&Number.isFinite(Date.parse(initialRelease.publishedAt))?'public':'draft';
}
if(process.argv[1]&&import.meta.url===pathToFileURL(process.argv[1]).href){
 const policy=JSON.parse(readFileSync('config/maintenance-release.json','utf8').replace(/^\uFEFF/,''));
 const initial=process.argv[3]?JSON.parse(readFileSync(process.argv[3],'utf8').replace(/^\uFEFF/,'')):null;
 console.log(releaseMode(policy,process.argv[2],initial));
}
