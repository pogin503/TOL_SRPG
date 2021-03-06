﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DxLibDLL;
using TOL_SRPG.Base;

namespace TOL_SRPG.App.ScriptConector
{
    //
    public class ActionStatusScriptConector : IDisposable
    {
        Action action;

        public ActionStatusScriptConector(Action action)
        {
            this.action = action;
        }
        
        public void ReleaseFreeze()
        {
            action.is_frease = false;
        }

        public void Dispose()
        {

        }
    }

    public class DrawEffectScriptConector : IDisposable
    {

        public class _Font : IDisposable
        {

            public Color main_color;
            public Color frame_color;


            public SFont sfont;
            public _Font( string font_family_name, int size, int frame_size )
            {
                //: base( font_name, size, thick, Antialiasing.Normal, edge_size )
                sfont = new SFont(font_family_name, size, 0, SFont.Antialiasing.Normal, frame_size);
                main_color = new Color(255, 255, 255, 255);
                frame_color = new Color(255, 255, 255, 255);
            }

            public void Dispose()
            {
                sfont.Dispose();
            }
        }

        List<_Font> fonts = new List<_Font>();
        static Dictionary<string, string> font_familys;


        public DrawEffectScriptConector()
        {
            if (font_familys == null)
            {
                font_familys = new Dictionary<string, string>();

                font_familys.Add("", GameMain.main_font_name_r);
                font_familys.Add("Bold", GameMain.main_font_name_b);
            }
        }

        // _Fontを返す、開放処理を簡略化したいので、スクリプト側で直接クラス生成をさせない
        public _Font Font(string font_family_name, int size, int frame_size)
        {
            var font = new _Font(font_family_name,size,frame_size);
            fonts.Add(font);
            return font;
        }

        public class Color
        {
            public uint color_uint = 0;
            public int color_a = 255;
            public Color( int a, int r, int g, int b )
            {
                color_a = a;
                color_uint = DX.GetColor(r,g,b);
            }
        }

        // キーと結びついたフォントファミリー名を取得する
        public static string GetFontFamilyNameByKey(string key = "")
        {
            if (!font_familys.ContainsKey(key))
            {
                return GameMain.main_font_name_r; // 対応キーがないのでレギュラー返しておく
            }

            return font_familys[key];
        }

        // 文字列の描画
        // DXライブラリの都合上、透明度はmain_colorのみ参照させる
        public void Text( int x, int y, string text, _Font font/*, Color main_color, Color frame_color*/ )
        {
            if (font.main_color.color_a == 255)
            {
                DX.DrawStringFToHandle(x, y, text, font.main_color.color_uint, font.sfont.GetHandle(), font.frame_color.color_uint);
            }
            else
            {
                DX.SetDrawBlendMode(DX.DX_BLENDMODE_ALPHA, font.main_color.color_a);
                DX.DrawStringFToHandle(x, y, text, font.main_color.color_uint, font.sfont.GetHandle(), font.frame_color.color_uint);
                DX.SetDrawBlendMode(DX.DX_BLENDMODE_NOBLEND, 0);
            }
        }

        /// <summary>
        /// 描画する文字列の横幅を取得する
        /// </summary>
        /// <param name="text"></param>
        /// <param name="font"></param>
        /// <returns></returns>
        public int GetTextWidth(string text, _Font font)
        {
            var res = DX.GetDrawStringWidthToHandle(text, text.Count(), font.sfont.GetHandle());
            return res;
        }

        // 指定したマップのマスから、スクリーン座標を取得する
        public IronPython.Runtime.PythonTuple GetScreenPositionByMapPos(int map_x, int map_y, double offset_x, double offset_y, double offset_z)
        {
            //var p = new Point(0, 0);

            float dx = 0, dy = 0, dz = 0;
            GameMain.GetInstance().g3d_map.Get3DPos(map_x, map_y, ref dx, ref dy, ref dz);

            dx += (float)offset_x;
            dy += (float)offset_y;
            dz += (float)offset_z;
            var dv = DX.VGet(dx, dy, dz);
            var v = DX.ConvWorldPosToScreenPos(dv);
            //p.X = (int)v.x;
            //p.Y = (int)v.y;

            var res = new IronPython.Runtime.PythonTuple(new[] { (int)v.x, (int)v.y });
            return res;
        }

        public void Dispose()
        {
            foreach( var i in fonts )
            {
                i.Dispose();
            }
            fonts.Clear();
        }
    }

    public class DrawEffect : Action
    {
        PythonScript python_script;
        DrawEffectScriptConector             draw;
        BattleMapEffectScriptConector.Effect effect;
        ActionStatusScriptConector           status;


        public DrawEffect(string script_path, dynamic param)
        {
            draw = new DrawEffectScriptConector();
            effect = new BattleMapEffectScriptConector.Effect(new ScriptManager(script_path));
            status = new ActionStatusScriptConector(this);

            python_script = new PythonScript(script_path,
                    (s) => {
                        s.SetVariable("param",  param);
                        s.SetVariable("draw",   draw);
                        s.SetVariable("effect", effect);
                        s.SetVariable("status", status);
                    }
                );
        }

        public override void Update()
        {
            if ( python_script.script.Update()==false )
            {
                is_end = true;
            }
        }

        public override void Draw()
        {
            python_script.script.Draw();
        }

        public override void Dispose()
        {
            base.Dispose();
            draw.Dispose();
            status.Dispose();
        }
    }
}
