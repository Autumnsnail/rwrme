import xml.etree.ElementTree as ET
import os
import re
from collections import defaultdict

def extract_key_values_from_svg(file_path):
    """
    从SVG文件中提取键值对，按节点类型->inkscape:label->键名->值的层级结构组织
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # 使用正则表达式查找所有包含desc的节点
        # 改进模式以提取inkscape:label属性
        pattern = r'<([a-zA-Z_:][a-zA-Z0-9_:]*)([^>]*)>.*?<desc[^>]*>([^<]+)</desc>'
        matches = re.findall(pattern, content, re.DOTALL | re.IGNORECASE)
        
        # 按节点类型->label->键名->值集合的层级组织数据
        result = defaultdict(lambda: defaultdict(lambda: defaultdict(set)))
        
        for parent_tag, attributes, desc_text in matches:
            # 清理父节点标签（去掉命名空间前缀）
            if ':' in parent_tag:
                parent_tag = parent_tag.split(':')[-1]
            
            # 提取inkscape:label属性
            label = "无label"
            label_match = re.search(r'inkscape:label\s*=\s*["\']([^"\']+)["\']', attributes)
            if label_match:
                label = label_match.group(1).strip()
            
            # 解析键值对（支持 key=value; 格式）
            # 改进模式以处理等号两边可能有空格的情况
            kv_pattern = r'([a-zA-Z_][a-zA-Z0-9_]*)\s*=\s*([^;]+)(?=\s*;|$)'
            kv_matches = re.findall(kv_pattern, desc_text)
            
            for key, value in kv_matches:
                key = key.strip()
                value = value.strip()
                result[parent_tag][label][key].add(value)
        
        return dict(result)
    
    except Exception as e:
        print(f"解析文件失败: {e}")
        return {}

def analyze_svg_directory(directory_path):
    """
    分析目录下所有SVG文件
    """
    if not os.path.exists(directory_path):
        print(f"目录不存在: {directory_path}")
        return
    
    # 查找所有SVG文件
    svg_files = []
    for root_dir, _, files in os.walk(directory_path):
        for file in files:
            if file.lower().endswith('.svg'):
                svg_files.append(os.path.join(root_dir, file))
    
    if not svg_files:
        print("未找到SVG文件")
        return
    
    print(f"找到 {len(svg_files)} 个SVG文件")
    
    # 合并所有文件的结果
    all_results = defaultdict(lambda: defaultdict(lambda: defaultdict(set)))
    
    for i, svg_file in enumerate(svg_files, 1):
        print(f"正在分析 ({i}/{len(svg_files)}): {os.path.basename(svg_file)}")
        file_results = extract_key_values_from_svg(svg_file)
        
        # 合并结果
        for node_type, labels_dict in file_results.items():
            for label, keys_dict in labels_dict.items():
                for key, values_set in keys_dict.items():
                    all_results[node_type][label][key].update(values_set)
    
    # 转换为普通字典并排序
    final_results = {}
    for node_type in sorted(all_results.keys()):
        labels_dict = {}
        for label in sorted(all_results[node_type].keys()):
            keys_dict = {}
            for key in sorted(all_results[node_type][label].keys()):
                keys_dict[key] = sorted(all_results[node_type][label][key])
            labels_dict[label] = keys_dict
        final_results[node_type] = labels_dict
    
    return final_results

def print_structured_results(results):
    """
    按层级结构打印结果
    """
    if not results:
        print("未找到任何键值对")
        return
    
    print("\n" + "="*80)
    print("SVG键值对三级分类结果")
    print("="*80)
    
    total_nodes = len(results)
    total_labels = sum(len(labels) for labels in results.values())
    total_keys = 0
    total_values = 0
    
    for node_type in sorted(results.keys()):
        print(f"\n节点类型: 【{node_type}】")
        
        labels_dict = results[node_type]
        for label in sorted(labels_dict.keys()):
            print(f"  └─ inkscape:label: {label}")
            
            keys_dict = labels_dict[label]
            for key in sorted(keys_dict.keys()):
                values = keys_dict[key]
                values_count = len(values)
                
                # 如果值太多，只显示前5个，其余的用...表示
                display_values = values
                if values_count > 5:
                    display_values = values[:5] + [f"...等{values_count}个值"]
                
                print(f"      ├─ {key}: {', '.join(display_values)}")
            
            # 该label的统计
            label_keys = len(keys_dict)
            label_values = sum(len(v) for v in keys_dict.values())
            print(f"      └─ 本label: {label_keys}个键，{label_values}个唯一值")
            
            total_keys += label_keys
            total_values += label_values
        
        # 该节点类型的统计
        node_labels = len(labels_dict)
        print(f"  ══ 本节点类型: {node_labels}个label")
    
    print(f"\n{'='*80}")
    print(f"总统计: {total_nodes}种节点类型，{total_labels}个label，{total_keys}个不同键，{total_values}个唯一值")
    print("="*80)

def save_results_to_file(results, output_file="svg_key_value_summary.txt"):
    """
    保存结果到文件
    """
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write("SVG键值对三级分类汇总\n")
        f.write("="*80 + "\n\n")
        
        total_nodes = len(results)
        total_labels = sum(len(labels) for labels in results.values())
        total_keys = 0
        total_values = 0
        
        for node_type in sorted(results.keys()):
            f.write(f"节点类型: 【{node_type}】\n")
            
            labels_dict = results[node_type]
            for label in sorted(labels_dict.keys()):
                f.write(f"  └─ inkscape:label: {label}\n")
                
                keys_dict = labels_dict[label]
                for key in sorted(keys_dict.keys()):
                    values = keys_dict[key]
                    values_count = len(values)
                    f.write(f"      ├─ {key}: {', '.join(values)}\n")
                
                # 该label的统计
                label_keys = len(keys_dict)
                label_values = sum(len(v) for v in keys_dict.values())
                f.write(f"      └─ 本label: {label_keys}个键，{label_values}个唯一值\n\n")
                
                total_keys += label_keys
                total_values += label_values
            
            # 该节点类型的统计
            node_labels = len(labels_dict)
            f.write(f"  本节点类型: {node_labels}个label\n\n")
        
        f.write("="*80 + "\n")
        f.write(f"总统计: {total_nodes}种节点类型，{total_labels}个label，{total_keys}个不同键，{total_values}个唯一值\n")
    
    print(f"\n结果已保存到: {output_file}")

def export_to_csv(results, output_file="svg_key_value_summary.csv"):
    """
    导出为CSV格式，便于进一步分析
    """
    import csv
    
    with open(output_file, 'w', encoding='utf-8', newline='') as f:
        writer = csv.writer(f)
        writer.writerow(['节点类型', 'inkscape:label', '键名', '值', '值个数'])
        
        for node_type in sorted(results.keys()):
            labels_dict = results[node_type]
            for label in sorted(labels_dict.keys()):
                keys_dict = labels_dict[label]
                for key in sorted(keys_dict.keys()):
                    values = keys_dict[key]
                    for value in sorted(values):
                        writer.writerow([node_type, label, key, value, len(values)])
    
    print(f"CSV格式已保存到: {output_file}")

# 主程序
if __name__ == "__main__":
    print("SVG键值对三级分类分析工具")
    print("="*80)
    print("分类层级: 节点类型 → inkscape:label → 键名 → 值")
    print("="*80)
    
    # 直接分析指定目录
    target_directory = input("请输入SVG文件所在目录路径: ").strip().strip('"\'')
    
    if not target_directory:
        target_directory = "."  # 默认当前目录
    
    results = analyze_svg_directory(target_directory)
    print_structured_results(results)
    
    # 询问是否保存结果
    if results:
        print("\n输出选项:")
        print("1. 保存为文本文件")
        print("2. 导出为CSV文件")
        print("3. 都保存")
        print("4. 不保存")
        
        save_option = input("请选择 (1/2/3/4): ").strip()
        
        if save_option == '1':
            save_results_to_file(results)
        elif save_option == '2':
            export_to_csv(results)
        elif save_option == '3':
            save_results_to_file(results)
            export_to_csv(results)
        else:
            print("未保存结果")